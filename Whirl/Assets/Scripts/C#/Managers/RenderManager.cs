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

    [System.NonSerialized] public bool programRunning = true;

    // Runtime registry (play mode)
    readonly List<RenderRB> _renderRBs = new();
    readonly List<SceneRigidBody> _sceneRBs = new();

    // Edit-mode registry
    readonly Dictionary<SceneRigidBody, RenderRB> _editMap = new();

    bool _requestInFlight;

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

        // --- RUNTIME PATH (unchanged logic, but uses GPU view-rect mapping) ---
        if (!programRunning) return;
        if (main == null)
        {
            var mainGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainGo != null) main = mainGo.GetComponent<Main>();
            if (main == null) return;
        }
        if (main.RBDataBuffer == null) return;
        EnsureViewCameraAssigned();
        if (_requestInFlight) return;

        if (!viewCamera.orthographic)
        {
            Debug.LogError("RenderManager: viewCamera must be Orthographic.");
            return;
        }

        Rect viewRect = GetCameraViewRect(viewCamera);
        Vector2 simDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        Vector2 simToWorldScale = new(viewRect.width / simDims.x, viewRect.height / simDims.y);
        Vector2 worldMin = viewRect.min;

        _requestInFlight = true;
        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            if (!programRunning) { _requestInFlight = false; return; }
            if (contents == null) { _requestInFlight = false; return; }

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
                // UVs are built using TransformedTiling in Build(); no per-frame rebuild required.
            }

#if UNITY_EDITOR
            if (contents.Length != _renderRBs.Count)
            {
                Debug.Log($"RenderManager: RBData length ({contents.Length}) != RenderRB count ({_renderRBs.Count}). Applied {count}.");
            }
#endif
            _requestInFlight = false;
        });
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

    // ----------------- RUNTIME API (unchanged, but keeps the view-rect mapping) -----------------

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
        if (!viewCamera.orthographic)
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
        return renderRB;
    }

    public void RegisterExisting(RenderRB renderRB, SceneRigidBody sceneRigidBody = null)
    {
        if (renderRB == null) return;
        if (_renderRBs.Contains(renderRB)) return;
        _renderRBs.Add(renderRB);
        _sceneRBs.Add(sceneRigidBody);
    }

    public void ClearRegistry()
    {
        _renderRBs.Clear();
        _sceneRBs.Clear();
    }

    public void UpdateAllTransformsFromGpuAsync()
    {
        if (!programRunning) return;
        if (main == null) return;
        if (main.RBDataBuffer == null) return;
        EnsureViewCameraAssigned();
        if (!viewCamera.orthographic)
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