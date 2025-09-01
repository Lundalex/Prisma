using System.Collections.Generic;
using UnityEngine;
using PM = ProgramManager;

[ExecuteAlways, DefaultExecutionOrder(100)]
public class RenderManager : MonoBehaviour
{
    public Main main;
    public Transform renderRoot;
    public RenderRB renderRbPrefab;
    public SceneManager sceneManager;

    [Header("Camera mapping")]
    [Tooltip("Orthographic camera whose view rect is mapped to the simulation rectangle [0..BoundaryDims].")]
    public Camera viewCamera;

    [Header("Prediction (gap fill)")]
    [Tooltip("If true, fill gaps between async fetches using last measured velocity / angular velocity.")]
    public bool predictionEnabled = true;

    [Tooltip("Maximum time to predict ahead when no new data has arrived (seconds). 0 = unlimited (use with care).")]
    [Min(0f)] public float maxPredictionSeconds = 0.25f;

    [Tooltip("If > 0, prediction horizon also scales with the last observed fetch interval. Example: 1.5 = predict up to 150% of last interval. 0 = disabled.")]
    [Min(0f)] public float adaptiveHorizonFactor = 1.5f;

    [Tooltip("Cap linear speed (sim units/sec). 0 = uncapped.")]
    [Min(0f)] public float maxLinearSpeed = 0f;

    [Tooltip("Cap angular speed (deg/sec). 0 = uncapped.")]
    [Min(0f)] public float maxAngularSpeedDeg = 0f;

    [Header("Async / Latency")]
    [Tooltip("Minimum time between issuing async requests (seconds). 0 = as fast as possible. Uses unscaled time so timeScale changes won't affect it.")]
    [Min(0f)] public float minRequestInterval = 0f;

    [Tooltip("Max concurrent async reads. 0 = Unlimited.")]
    [Min(0)] public int maxRequestsInFlight = 0; // 0 = unlimited

    [Header("Robustness")]
    [Tooltip("Drop async callbacks that return out of order (older than the latest applied request). Strongly recommended when concurrency > 1.")]
    public bool dropOutOfOrderCallbacks = true;

    [System.NonSerialized] public bool programRunning = true;

    // Runtime registry (play mode)
    readonly List<RenderRB> _renderRBs = new();
    readonly List<SceneRigidBody> _sceneRBs = new();

    // Per-RB tracking for velocity-based prediction
    class TrackState
    {
        public bool initialized;
        public Vector2 posSim;     // last received sample (sim space)
        public float rotDeg;       // last received sample (degrees)
        public double sampleTimeUS; // Time.unscaledTime of last sample
        public Vector2 velSim;     // estimated linear velocity (sim units/sec)
        public float angVelDeg;    // estimated angular velocity (deg/sec)
    }

    readonly List<TrackState> _track = new();

    // Edit-mode registry
    readonly Dictionary<SceneRigidBody, RenderRB> _editMap = new();

    // Async / latency tracking (global)
    int _requestsInFlight;
    double _lastRequestTimeUS;
    double _lastSampleBatchTimeUS; // time of last APPLIED batch callback
    float _lastObservedInterval;   // seconds between last two APPLIED batch callbacks

    // Request sequencing (for out-of-order protection)
    long _issueSeq;           // incremented on IssueAsyncRead()
    long _lastAppliedSeq;     // last sequence id we accepted

    void OnDisable()
    {
        // Clean up editor-created preview meshes
        if (!Application.isPlaying)
        {
            foreach (var kv in _editMap)
            {
                if (kv.Value) DestroyImmediate(kv.Value.gameObject);
            }
            _editMap.Clear();
        }
    }

    void Update()
    {
        // --- EDIT MODE PREVIEW ---
        if (!Application.isPlaying)
        {
            EditModeUpdate();
            return;
        }

        // --- RUNTIME PATH (prediction + GPU view-rect mapping) ---
        if (!programRunning) return;
        if (main == null)
        {
            var mainGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainGo != null) main = mainGo.GetComponent<Main>();
            if (main == null) return;
        }
        if (main.RBDataBuffer == null) return;
        EnsureViewCameraAssigned();

        if (!viewCamera || !viewCamera.orthographic)
        {
            Debug.LogError("RenderManager: viewCamera must be assigned and Orthographic.");
            return;
        }

        Rect viewRect = GetCameraViewRect(viewCamera);
        Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        Vector2 simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
        Vector2 worldMin = viewRect.min;

        EnsureTrackCapacity(_renderRBs.Count);

        // Render predicted (or last known) poses every frame
        int rc = _renderRBs.Count;
        double nowUS = Time.unscaledTimeAsDouble;
        float horizon = ComputePredictionHorizonSeconds();

        for (int i = 0; i < rc; i++)
        {
            var rrb = _renderRBs[i];
            if (rrb == null) continue;

            var st = _track[i];
            if (!st.initialized) continue; // wait for first sample

            Vector2 posSim = st.posSim;
            float rotDeg = st.rotDeg;

            if (predictionEnabled)
            {
                float dt = (float)Mathf.Max(0f, (float)(nowUS - st.sampleTimeUS));
                float dtClamped = (horizon > 0f) ? Mathf.Min(dt, horizon) : dt;

                // Predict using last measured velocities
                posSim += st.velSim * dtClamped;
                rotDeg += st.angVelDeg * dtClamped;
            }

            // Map sim → world and apply
            Vector2 worldCenter = new(
                worldMin.x + posSim.x * simToWorldScale.x,
                worldMin.y + posSim.y * simToWorldScale.y
            );
            rrb.UpdatePoseWorld(worldCenter, rotDeg, simToWorldScale);
        }

        // Issue async requests based on unscaled time (robust to timeScale changes)
        bool unlimited = (maxRequestsInFlight <= 0);
        bool underCap = _requestsInFlight < maxRequestsInFlight;
        if ((unlimited || underCap) && (minRequestInterval <= 0f || (Time.unscaledTimeAsDouble - _lastRequestTimeUS) >= minRequestInterval))
        {
            IssueAsyncRead();
        }
    }

    void IssueAsyncRead()
    {
        if (main == null || main.RBDataBuffer == null) return;

        _requestsInFlight = Mathf.Max(0, _requestsInFlight); // safety
        _requestsInFlight++;
        _lastRequestTimeUS = Time.unscaledTimeAsDouble;

        long mySeq = ++_issueSeq;

        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            // Make sure we always decrement the in-flight count even if something throws
            try
            {
                _requestsInFlight = Mathf.Max(0, _requestsInFlight - 1);
                if (!programRunning || contents == null) return;

                // Drop out-of-order responses if requested
                if (dropOutOfOrderCallbacks && mySeq < _lastAppliedSeq)
                    return;

                double nowUS = Time.unscaledTimeAsDouble;

                // Track observed batch interval on ACCEPTED samples only
                if (_lastSampleBatchTimeUS > 0.0)
                    _lastObservedInterval = Mathf.Max(0.0001f, (float)(nowUS - _lastSampleBatchTimeUS));
                _lastSampleBatchTimeUS = nowUS;
                _lastAppliedSeq = mySeq;

                EnsureTrackCapacity(_renderRBs.Count);

                int count = Mathf.Min(contents.Length, _renderRBs.Count);
                for (int i = 0; i < count; i++)
                {
                    var data = contents[i];
                    var st = _track[i];

                    Vector2 newPos = new Vector2(data.pos.x, data.pos.y);
                    float newRot = data.totRot; // Assumed degrees. Convert if your data is radians.

                    if (!st.initialized)
                    {
                        st.initialized = true;
                        st.posSim = newPos;
                        st.rotDeg = newRot;
                        st.sampleTimeUS = nowUS;
                        st.velSim = Vector2.zero;
                        st.angVelDeg = 0f;
                    }
                    else
                    {
                        float dt = Mathf.Max(0.0001f, (float)(nowUS - st.sampleTimeUS));

                        // Estimate velocities from last two samples
                        Vector2 vel = (newPos - st.posSim) / dt;
                        float angVel = Mathf.DeltaAngle(st.rotDeg, newRot) / dt;

                        // Optional caps
                        if (maxLinearSpeed > 0f) vel = Vector2.ClampMagnitude(vel, maxLinearSpeed);
                        if (maxAngularSpeedDeg > 0f) angVel = Mathf.Clamp(angVel, -maxAngularSpeedDeg, maxAngularSpeedDeg);

                        st.velSim = vel;
                        st.angVelDeg = angVel;

                        st.posSim = newPos;
                        st.rotDeg = newRot;
                        st.sampleTimeUS = nowUS;
                    }
                }

#if UNITY_EDITOR
                if (contents.Length != _renderRBs.Count)
                {
                    Debug.Log($"RenderManager: RBData length ({contents.Length}) != RenderRB count ({_renderRBs.Count}). Applied {count}.");
                }
#endif
            }
            catch (System.SystemException ex)
            {
                Debug.LogException(ex);
            }
        });
    }

    float ComputePredictionHorizonSeconds()
    {
        // Combine fixed horizon and adaptive horizon (take the minimum positive one if both are set)
        float fixedH = (maxPredictionSeconds > 0f) ? maxPredictionSeconds : -1f;
        float adaptiveH = (adaptiveHorizonFactor > 0f && _lastObservedInterval > 0f)
            ? adaptiveHorizonFactor * _lastObservedInterval
            : -1f;

        if (fixedH > 0f && adaptiveH > 0f) return Mathf.Min(fixedH, adaptiveH);
        if (fixedH > 0f) return fixedH;
        if (adaptiveH > 0f) return adaptiveH;
        return 0f; // 0 = unlimited prediction (use with care)
    }

    void EnsureTrackCapacity(int desired)
    {
        while (_track.Count < desired) _track.Add(new TrackState());
        // If desired < current, we keep old entries; ClearRegistry() resets completely.
    }

    // ----------------- EDIT MODE -----------------

    void EditModeUpdate()
    {
        EnsureViewCameraAssigned();

        // Build/update preview renderers for each SceneRigidBody in the scene.
        var srs = FindObjectsByType<SceneRigidBody>(FindObjectsSortMode.None);

        // Remove stale entries
        var stillPresent = new HashSet<SceneRigidBody>(srs);
        var toRemove = new List<SceneRigidBody>();
        foreach (var kv in _editMap)
        {
            if (kv.Key == null || !stillPresent.Contains(kv.Key))
                toRemove.Add(kv.Key);
        }
        foreach (var sr in toRemove)
        {
            if (_editMap.TryGetValue(sr, out var rrb) && rrb != null)
                DestroyImmediate(rrb.gameObject);
            _editMap.Remove(sr);
        }

        // Add/update
        foreach (var sr in srs)
        {
            if (sr == null || !sr.gameObject.activeInHierarchy) continue;

            if (!_editMap.TryGetValue(sr, out var rrb) || rrb == null)
            {
                rrb = CreateEditRenderer(sr);
                _editMap[sr] = rrb;
            }
            UpdateEditRendererShapeAndPose(sr, rrb);
        }
    }

    RenderRB CreateEditRenderer(SceneRigidBody sr)
    {
        RenderRB renderRB;
        if (renderRbPrefab != null)
        {
            renderRB = Instantiate(renderRbPrefab, null, true);
            renderRB.name = $"RenderRB_EDIT_{_editMap.Count:000}";
        }
        else
        {
            var go = new GameObject($"RenderRB_EDIT_{_editMap.Count:000}");
            renderRB = go.AddComponent<RenderRB>();
        }

        renderRB.gameObject.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
        if (renderRoot != null) renderRB.transform.SetParent(renderRoot, true);

        renderRB.viewCamera = viewCamera;
        return renderRB;
    }

    void UpdateEditRendererShapeAndPose(SceneRigidBody sr, RenderRB rrb)
    {
        if (sr == null || rrb == null) return;

        // Ensure its polygon data is fresh (matches how the editor gizmos do it)
        sr.SetPolygonData();

        // Build path in world space
        Vector2[] path = GetPrimaryPathWorld(sr);
        if (path == null || path.Length < 3) return;

        // Same offset convention we use at runtime
        Vector2 boundsOffset = GetBoundsOffsetSafe();

        Vector2 transformedRBPos = (Vector2)sr.transform.position + boundsOffset;
        var vectors = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
            vectors[i] = path[i] + boundsOffset - transformedRBPos;

        // Optional subdivision if SceneManager is available and requested
        if (sceneManager != null && sr.addInBetweenPoints)
            SceneManager.AddInBetweenPoints(ref vectors, sr.doRecursiveSubdivisison, sr.minDstForSubDivision);

        // Update mesh
        rrb.basePointsSim = new List<Vector2>(vectors);
        rrb.Build();

        // Pose in world using the same GPU *view* mapping when possible
        Vector2 simToWorldScale;
        Vector2 worldCenter;

        if (main != null && viewCamera != null && viewCamera.orthographic)
        {
            Rect viewRect = GetCameraViewRect(viewCamera);
            Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
            simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
            Vector2 worldMin = viewRect.min;

            worldCenter = new(
                worldMin.x + transformedRBPos.x * simToWorldScale.x,
                worldMin.y + transformedRBPos.y * simToWorldScale.y
            );
        }
        else
        {
            // Fallback: 1:1 world<->sim (still shows correctly in scene view)
            simToWorldScale = Vector2.one;
            worldCenter = transformedRBPos;
        }

        // We already baked the SceneRigidBody's transform into the points, so use 0 rotation here.
        rrb.UpdatePoseWorld(worldCenter, 0f, simToWorldScale);
    }

    // ----------------- RUNTIME API (unchanged externally) -----------------

    public RenderRB AddRigidBody(SceneRigidBody sceneRigidBody)
    {
        if (sceneRigidBody == null) return null;
        if (main == null)
        {
            var mainGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainGo != null) main = mainGo.GetComponent<Main>();
            if (main == null) return null;
        }
        EnsureViewCameraAssigned();
        if (!viewCamera || !viewCamera.orthographic)
        {
            Debug.LogError("RenderManager: assign an Orthographic 'viewCamera' in the Inspector.");
            return null;
        }

        EnsureSceneManagerRef();
        Vector2 boundsOffset = GetBoundsOffsetSafe();

        Vector2[] path = GetPrimaryPathWorld(sceneRigidBody);
        if (path == null || path.Length < 3)
        {
            Debug.LogWarning($"RenderManager: SceneRigidBody '{sceneRigidBody.name}' has no valid path to render.");
            return null;
        }

        Vector2 transformedRBPos = (Vector2)sceneRigidBody.transform.position + boundsOffset;
        var vectors = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
            vectors[i] = path[i] + boundsOffset - transformedRBPos;

        if (sceneRigidBody.addInBetweenPoints)
            SceneManager.AddInBetweenPoints(ref vectors, sceneRigidBody.doRecursiveSubdivisison, sceneRigidBody.minDstForSubDivision);

        Vector2 rbPosSim = transformedRBPos;
        sceneRigidBody.ComputeInertiaAndBalanceRigidBody(ref vectors, ref rbPosSim, boundsOffset, null);

        RenderRB renderRB;
        if (renderRbPrefab != null)
        {
            renderRB = Instantiate(renderRbPrefab, null, true);
            renderRB.name = $"RenderRB_{_renderRBs.Count:000}";
        }
        else
        {
            var go = new GameObject($"RenderRB_{_renderRBs.Count:000}");
            renderRB = go.AddComponent<RenderRB>();
        }

        if (renderRoot != null) renderRB.transform.SetParent(renderRoot, true);

        renderRB.basePointsSim = new List<Vector2>(vectors);
        renderRB.viewCamera = viewCamera; // provide camera so TransformedTiling can compute screen view width
        renderRB.Build();

        Rect viewRect = GetCameraViewRect(viewCamera);
        Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        Vector2 simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
        Vector2 worldMin = viewRect.min;

        Vector2 worldCenter = new(
            worldMin.x + rbPosSim.x * simToWorldScale.x,
            worldMin.y + rbPosSim.y * simToWorldScale.y
        );
        renderRB.UpdatePoseWorld(worldCenter, 0f, simToWorldScale);

        _renderRBs.Add(renderRB);
        _sceneRBs.Add(sceneRigidBody);
        EnsureTrackCapacity(_renderRBs.Count);
        return renderRB;
    }

    public void RegisterExisting(RenderRB renderRB, SceneRigidBody sceneRigidBody = null)
    {
        if (renderRB == null) return;
        if (_renderRBs.Contains(renderRB)) return;
        _renderRBs.Add(renderRB);
        _sceneRBs.Add(sceneRigidBody);
        EnsureTrackCapacity(_renderRBs.Count);
    }

    public void ClearRegistry()
    {
        _renderRBs.Clear();
        _sceneRBs.Clear();
        _track.Clear();
        _requestsInFlight = 0;
        _lastObservedInterval = 0f;
        _lastSampleBatchTimeUS = 0.0;
        _issueSeq = 0;
        _lastAppliedSeq = 0;
    }

    public void UpdateAllTransformsFromGpuAsync()
    {
        if (!programRunning) return;
        if (main == null) return;
        if (main.RBDataBuffer == null) return;
        EnsureViewCameraAssigned();
        if (!viewCamera || !viewCamera.orthographic)
        {
            Debug.LogError("RenderManager: viewCamera must be Orthographic.");
            return;
        }

        Rect viewRect = GetCameraViewRect(viewCamera);
        Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        Vector2 simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
        Vector2 worldMin = viewRect.min;

        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            if (!programRunning || contents == null) return;
            int count = Mathf.Min(contents.Length, _renderRBs.Count);
            for (int i = 0; i < count; i++)
            {
                var data = contents[i];
                var rrb  = _renderRBs[i];
                if (rrb == null) continue;

                Vector2 worldCenter = new Vector2(
                    worldMin.x + data.pos.x * simToWorldScale.x,
                    worldMin.y + data.pos.y * simToWorldScale.y
                );
                rrb.UpdatePoseWorld(worldCenter, data.totRot, simToWorldScale);
            }
        });
    }

    // ————— helpers —————

    void EnsureSceneManagerRef()
    {
        if (sceneManager != null) return;
        sceneManager = FindFirstObjectByType<SceneManager>();
    }

    void EnsureViewCameraAssigned()
    {
        if (viewCamera != null) return;

        var tagged = GameObject.FindGameObjectWithTag("RBCamera");
        if (tagged) viewCamera = tagged.GetComponent<Camera>();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    Vector2 GetBoundsOffsetSafe()
    {
        if (sceneManager == null) return Vector2.zero;
        var t = sceneManager.transform;
        return new Vector2(
            t.localScale.x * 0.5f - t.position.x,
            t.localScale.y * 0.5f - t.position.y
        );
    }

    static Vector2[] GetPrimaryPathWorld(SceneRigidBody sr)
    {
        var poly = sr.GetComponent<PolygonCollider2D>();
        if (poly == null || poly.pathCount == 0) return null;

        var local = poly.GetPath(0);
        var world = new Vector2[local.Length];
        for (int i = 0; i < local.Length; i++)
            world[i] = sr.transform.TransformPoint(local[i]);
        return world;
    }

    // The raw camera rect (centered on camera position)
    static Rect GetCameraWorldRect(Camera cam)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 p = cam.transform.position;
        return new Rect(p.x - halfW, p.y - halfH, halfW * 2f, halfH * 2f);
    }

    // The GPU view rectangle (camera rect scaled by ScreenToViewFactorScene).
    static Rect GetCameraViewRect(Camera cam)
    {
        Rect full = GetCameraWorldRect(cam);
        Vector2 center = full.center;
        Vector2 factor = (PM.Instance != null) ? PM.Instance.ScreenToViewFactorScene : Vector2.one;

        // shrink (or expand) the rect around the center according to the factor
        Vector2 half = new(full.width * 0.5f * factor.x, full.height * 0.5f * factor.y);
        return new Rect(center - half, half * 2f);
    }
}