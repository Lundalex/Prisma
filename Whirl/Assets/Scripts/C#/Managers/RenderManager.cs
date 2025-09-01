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
    public Camera viewCamera;

    [Header("Prediction / Moving Averages")]
    [Range(0f, 1f)] public float latencyMovingAverageAlpha = 0.25f;
    [Range(0f, 1f)] public float intervalMovingAverageAlpha = 0.25f;
    [Range(0f, 1f)] public float inTransitMovingAverageAlpha = 0.2f;
    [Min(0f)] public float horizonFactor = 1.5f;

    [Header("Async / Latency")]
    [Min(0f)] public float minRequestInterval = 0f;
    [Min(0)]  public int   maxRequestsInFlight = 0;

    [Header("Robustness")]
    public bool dropOutOfOrderCallbacks = true;

    [System.NonSerialized] public bool programRunning = true;

    readonly List<RenderRB> _renderRBs = new();
    readonly List<SceneRigidBody> _sceneRBs = new();

    class TrackState
    {
        public bool initialized;
        public Vector2 posSim;
        public float rotDeg;
        public double sampleTimeUS;
        public Vector2 velSim;
        public float angVelDeg;
    }

    readonly List<TrackState> _track = new();
    readonly Dictionary<SceneRigidBody, RenderRB> _editMap = new();

    int _requestsInFlight;
    double _lastRequestTimeUS;
    double _lastSampleBatchTimeUS;

    float _latencyMovingAverage;
    float _intervalMovingAverage;
    float _inTransitMovingAverage;

    long _issueSeq;
    long _lastAppliedSeq;

    void OnDisable()
    {
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
        if (!Application.isPlaying)
        {
            EditModeUpdate();
            return;
        }

        if (!programRunning) return;
        if (main == null)
        {
            var mainGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainGo != null) main = mainGo.GetComponent<Main>();
            if (main == null) return;
        }
        if (main.RBDataBuffer == null) return;

        EnsureViewCameraAssigned();
        if (!viewCamera || !viewCamera.orthographic) return;

        Rect viewRect = GetCameraViewRect(viewCamera);
        Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        Vector2 simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
        Vector2 worldMin = viewRect.min;

        EnsureTrackCapacity(_renderRBs.Count);

        int rc = _renderRBs.Count;
        double nowUS = Time.unscaledTimeAsDouble;
        float horizon = ComputeHorizonSeconds();

        for (int i = 0; i < rc; i++)
        {
            var rrb = _renderRBs[i];
            if (rrb == null) continue;

            var st = _track[i];
            if (!st.initialized) continue;

            float dt = Mathf.Max(0f, (float)(nowUS - st.sampleTimeUS));
            float dtClamped = (horizon > 0f) ? Mathf.Min(dt, horizon) : dt;

            Vector2 posSim = st.posSim + st.velSim * dtClamped;
            float   rotDeg = st.rotDeg + st.angVelDeg * dtClamped;

            Vector2 worldCenter = new(
                worldMin.x + posSim.x * simToWorldScale.x,
                worldMin.y + posSim.y * simToWorldScale.y
            );
            rrb.UpdatePoseWorld(worldCenter, rotDeg, simToWorldScale);
        }

        bool unlimited = (maxRequestsInFlight <= 0);
        bool underCap = _requestsInFlight < maxRequestsInFlight;
        if ((unlimited || underCap) && (minRequestInterval <= 0f || (Time.unscaledTimeAsDouble - _lastRequestTimeUS) >= minRequestInterval))
            IssueAsyncRead();
    }

    void IssueAsyncRead()
    {
        if (main == null || main.RBDataBuffer == null) return;

        _requestsInFlight = Mathf.Max(0, _requestsInFlight);
        _requestsInFlight++;
        _lastRequestTimeUS = Time.unscaledTimeAsDouble;

        UpdateInTransitMovingAverage(_requestsInFlight);

        long mySeq = ++_issueSeq;
        double issuedAtUS = _lastRequestTimeUS;

        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            try
            {
                _requestsInFlight = Mathf.Max(0, _requestsInFlight - 1);
                UpdateInTransitMovingAverage(_requestsInFlight);

                if (!programRunning || contents == null) return;
                if (dropOutOfOrderCallbacks && mySeq < _lastAppliedSeq) return;

                double nowUS = Time.unscaledTimeAsDouble;

                float thisLatency = Mathf.Max(0.0001f, (float)(nowUS - issuedAtUS));
                if (_latencyMovingAverage <= 0f) _latencyMovingAverage = thisLatency;
                else _latencyMovingAverage += latencyMovingAverageAlpha * (thisLatency - _latencyMovingAverage);

                if (_lastSampleBatchTimeUS > 0.0)
                {
                    float thisInterval = Mathf.Max(0.0001f, (float)(nowUS - _lastSampleBatchTimeUS));
                    if (_intervalMovingAverage <= 0f) _intervalMovingAverage = thisInterval;
                    else _intervalMovingAverage += intervalMovingAverageAlpha * (thisInterval - _intervalMovingAverage);
                }
                _lastSampleBatchTimeUS = nowUS;
                _lastAppliedSeq = mySeq;

                float effectiveLatency = GetEffectiveLatencySeconds();

                EnsureTrackCapacity(_renderRBs.Count);

                int count = Mathf.Min(contents.Length, _renderRBs.Count);
                for (int i = 0; i < count; i++)
                {
                    var data = contents[i];
                    var st = _track[i];

                    Vector2 newPos = new(data.pos.x, data.pos.y);
                    float newRot = data.totRot;

                    if (!st.initialized)
                    {
                        st.initialized = true;
                        st.posSim = newPos;
                        st.rotDeg = newRot;
                        st.sampleTimeUS = nowUS - effectiveLatency;
                        st.velSim = Vector2.zero;
                        st.angVelDeg = 0f;
                    }
                    else
                    {
                        float dt = Mathf.Max(0.0001f, (float)((nowUS - effectiveLatency) - st.sampleTimeUS));
                        st.velSim = (newPos - st.posSim) / dt;
                        st.angVelDeg = Mathf.DeltaAngle(st.rotDeg, newRot) / dt;

                        st.posSim = newPos;
                        st.rotDeg = newRot;
                        st.sampleTimeUS = nowUS - effectiveLatency;
                    }
                }
            }
            catch (System.SystemException ex)
            {
                Debug.LogException(ex);
            }
        });
    }

    float GetEffectiveLatencySeconds()
    {
        float denom = Mathf.Max(1f, _inTransitMovingAverage);
        return (_latencyMovingAverage > 0f) ? (_latencyMovingAverage / denom) : 0f;
    }

    void UpdateInTransitMovingAverage(int currentInFlight)
    {
        float value = Mathf.Max(0, currentInFlight);
        if (_inTransitMovingAverage <= 0f) _inTransitMovingAverage = value;
        else _inTransitMovingAverage += inTransitMovingAverageAlpha * (value - _inTransitMovingAverage);
    }

    float ComputeHorizonSeconds()
    {
        if (horizonFactor > 0f && _intervalMovingAverage > 0f)
            return horizonFactor * _intervalMovingAverage;
        return 0f;
    }

    void EnsureTrackCapacity(int desired)
    {
        while (_track.Count < desired) _track.Add(new TrackState());
    }

    void EditModeUpdate()
    {
        EnsureViewCameraAssigned();

        var srs = FindObjectsByType<SceneRigidBody>(FindObjectsSortMode.None);

        var stillPresent = new HashSet<SceneRigidBody>(srs);
        var toRemove = new List<SceneRigidBody>();
        foreach (var kv in _editMap)
            if (kv.Key == null || !stillPresent.Contains(kv.Key)) toRemove.Add(kv.Key);

        foreach (var sr in toRemove)
        {
            if (_editMap.TryGetValue(sr, out var rrb) && rrb != null)
                DestroyImmediate(rrb.gameObject);
            _editMap.Remove(sr);
        }

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

        sr.SetPolygonData();

        Vector2[] path = GetPrimaryPathWorld(sr);
        if (path == null || path.Length < 3) return;

        Vector2 boundsOffset = GetBoundsOffsetSafe();

        Vector2 transformedRBPos = (Vector2)sr.transform.position + boundsOffset;
        var vectors = new Vector2[path.Length];
        for (int i = 0; i < path.Length; i++)
            vectors[i] = path[i] + boundsOffset - transformedRBPos;

        if (sceneManager != null && sr.addInBetweenPoints)
            SceneManager.AddInBetweenPoints(ref vectors, sr.doRecursiveSubdivisison, sr.minDstForSubDivision);

        rrb.basePointsSim = new List<Vector2>(vectors);
        rrb.Build();

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
            simToWorldScale = Vector2.one;
            worldCenter = transformedRBPos;
        }

        rrb.UpdatePoseWorld(worldCenter, 0f, simToWorldScale);
    }

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
        if (!viewCamera || !viewCamera.orthographic) return null;

        EnsureSceneManagerRef();
        Vector2 boundsOffset = GetBoundsOffsetSafe();

        Vector2[] path = GetPrimaryPathWorld(sceneRigidBody);
        if (path == null || path.Length < 3) return null;

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
        renderRB.viewCamera = viewCamera;
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
        _lastSampleBatchTimeUS = 0.0;
        _issueSeq = 0;
        _lastAppliedSeq = 0;

        _latencyMovingAverage = 0f;
        _intervalMovingAverage = 0f;
        _inTransitMovingAverage = 0f;
    }

    public void UpdateAllTransformsFromGpuAsync()
    {
        if (!programRunning || main == null || main.RBDataBuffer == null) return;
        EnsureViewCameraAssigned();
        if (!viewCamera || !viewCamera.orthographic) return;

        Rect viewRect = GetCameraViewRect(viewCamera);
        Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        Vector2 simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
        Vector2 worldMin = viewRect.min;

        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            if (!programRunning || contents == null) return;
            int count = Mathf.Min(contents.Length, _renderRBs.Count);
            double nowUS = Time.unscaledTimeAsDouble;
            float horizon = ComputeHorizonSeconds();

            for (int i = 0; i < count; i++)
            {
                var st  = (i < _track.Count) ? _track[i] : null;
                var rrb = _renderRBs[i];
                if (rrb == null) continue;

                Vector2 posSim;
                float rotDeg;

                if (st != null && st.initialized)
                {
                    float dt = Mathf.Max(0f, (float)(nowUS - st.sampleTimeUS));
                    float dtClamped = (horizon > 0f) ? Mathf.Min(dt, horizon) : dt;
                    posSim = st.posSim + st.velSim * dtClamped;
                    rotDeg = st.rotDeg + st.angVelDeg * dtClamped;
                }
                else
                {
                    var data = contents[i];
                    posSim = new Vector2(data.pos.x, data.pos.y);
                    rotDeg = data.totRot;
                }

                Vector2 worldCenter = new(
                    worldMin.x + posSim.x * simToWorldScale.x,
                    worldMin.y + posSim.y * simToWorldScale.y
                );
                rrb.UpdatePoseWorld(worldCenter, rotDeg, simToWorldScale);
            }
        });
    }

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

    static Rect GetCameraWorldRect(Camera cam)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;
        Vector3 p = cam.transform.position;
        return new Rect(p.x - halfW, p.y - halfH, halfW * 2f, halfH * 2f);
    }

    static Rect GetCameraViewRect(Camera cam)
    {
        Rect full = GetCameraWorldRect(cam);
        Vector2 center = full.center;
        Vector2 factor = (PM.Instance != null) ? PM.Instance.ScreenToViewFactorScene : Vector2.one;
        Vector2 half = new(full.width * 0.5f * factor.x, full.height * 0.5f * factor.y);
        return new Rect(center - half, half * 2f);
    }
}