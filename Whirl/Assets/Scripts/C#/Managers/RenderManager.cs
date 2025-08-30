using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(100)]
public class RenderManager : MonoBehaviour
{
    public Main main;
    public Transform renderRoot;
    public bool programRunning = true;
    public RenderRB renderRbPrefab;
    public SceneManager sceneManager;

    readonly List<RenderRB> _renderRBs = new();
    readonly List<SceneRigidBody> _sceneRBs = new();

    bool _requestInFlight;

    void Update()
    {
        if (!programRunning) return;
        if (main == null)
        {
            var mainGo = GameObject.FindGameObjectWithTag("MainCamera");
            if (mainGo != null) main = mainGo.GetComponent<Main>();
            if (main == null) return;
        }
        if (main.RBDataBuffer == null) return;
        if (_requestInFlight) return;

        _requestInFlight = true;
        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            if (!programRunning) { _requestInFlight = false; return; }
            if (contents == null) { _requestInFlight = false; return; }

            int count = Mathf.Min(contents.Length, _renderRBs.Count);
            for (int i = 0; i < count; i++)
            {
                var data = contents[i];
                var rrb = _renderRBs[i];
                if (rrb == null) continue;

                rrb.transform.localPosition = new Vector3(data.pos.x, data.pos.y, rrb.transform.localPosition.z);
                float deg = data.totRot * Mathf.Rad2Deg;
                rrb.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
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

    public RenderRB AddRigidBody(SceneRigidBody sceneRigidBody)
    {
        if (sceneRigidBody == null) return null;

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
        {
            vectors[i] = path[i] + boundsOffset - transformedRBPos;
        }

        if (sceneRigidBody.addInBetweenPoints)
        {
            SceneManager.AddInBetweenPoints(ref vectors, sceneRigidBody.doRecursiveSubdivisison, sceneRigidBody.minDstForSubDivision);
        }

        Vector2 rbPos = transformedRBPos;
        sceneRigidBody.ComputeInertiaAndBalanceRigidBody(ref vectors, ref rbPos, boundsOffset, null);

        RenderRB renderRB;
        if (renderRbPrefab != null)
        {
            renderRB = Instantiate(renderRbPrefab, renderRoot, false);
            renderRB.name = $"RenderRB_{_renderRBs.Count:000}";
        }
        else
        {
            var go = new GameObject($"RenderRB_{_renderRBs.Count:000}");
            if (renderRoot != null) go.transform.SetParent(renderRoot, false);
            renderRB = go.AddComponent<RenderRB>();
        }

        renderRB.Build(new List<Vector2>(vectors), null, null, null);
        renderRB.transform.localPosition = new Vector3(rbPos.x, rbPos.y, renderRB.transform.localPosition.z);

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

        ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents =>
        {
            if (!programRunning || contents == null) return;
            int count = Mathf.Min(contents.Length, _renderRBs.Count);
            for (int i = 0; i < count; i++)
            {
                var data = contents[i];
                var rrb = _renderRBs[i];
                if (rrb == null) continue;

                rrb.transform.localPosition = new Vector3(data.pos.x, data.pos.y, rrb.transform.localPosition.z);
                float deg = data.totRot * Mathf.Rad2Deg;
                rrb.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
            }
        });
    }

    void EnsureSceneManagerRef()
    {
        if (sceneManager != null) return;
        sceneManager = FindFirstObjectByType<SceneManager>();
    }

    Vector2 GetBoundsOffsetSafe()
    {
        if (sceneManager == null)
            return Vector2.zero;

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
        {
            world[i] = sr.transform.TransformPoint(local[i]);
        }
        return world;
    }
}