using System.Collections.Generic;
using UnityEngine;
using PM = ProgramManager;

[DisallowMultipleComponent, ExecuteAlways]
[RequireComponent(typeof(MeshFilter)), RequireComponent(typeof(MeshRenderer))]
public class RenderRB : MonoBehaviour
{
    public List<Vector2> basePointsSim = new() { new(-1, -1), new(1, -1), new(1, 1), new(-1, 1) };

    public Material material;
    public float tiling = 1f;          // repeats per SCREEN VIEW WIDTH (GPU view rect)
    public Vector2 uvOffset = Vector2.zero;
    public Camera viewCamera;          // assigned by RenderManager; falls back to Camera tagged "RBCamera" or main

    Mesh _mesh; MeshFilter _mf; MeshRenderer _mr;
    int _lastPtsHash = int.MinValue;
    Material _lastMat;
    float _lastTiling = float.NaN;
    Vector2 _lastUv = new(float.NaN, float.NaN);

    // cached from last pose update; needed to convert sim units -> world units in TransformedTiling
    Vector2 _lastSimToWorldScale = Vector2.one;

    void Awake()
    {
        _mf = GetComponent<MeshFilter>();
        _mr = GetComponent<MeshRenderer>();
        _mesh = new Mesh { name = "RBMesh2D" };
    }

#if UNITY_EDITOR
    void OnEnable() => Build();
    void Update()
    {
        int h = PointsHash(basePointsSim);
        if (h != _lastPtsHash || material != _lastMat || tiling != _lastTiling || uvOffset != _lastUv)
        {
            Build();
        }
    }
#endif

    static int PointsHash(List<Vector2> pts)
    {
        if (pts == null) return 0;
        unchecked { int h = 17; for (int i = 0; i < pts.Count; i++) h = h * 31 + pts[i].GetHashCode(); return h; }
    }

    public float TransformedTiling()
    {
        // Use the GPU's *view* width (Camera rect scaled by ScreenToViewFactorScene.x) so UVs line up with the compute-rendered image.
        var cam = viewCamera;
        if (cam == null)
        {
            var tagCam = GameObject.FindGameObjectWithTag("RBCamera");
            cam = tagCam ? tagCam.GetComponent<Camera>() : Camera.main;
        }
        if (cam == null) return tiling; // safe fallback

        float factorX = PM.Instance != null ? PM.Instance.ScreenToViewFactorScene.x : 1f;
        float screenViewWidthWorld = 2f * cam.orthographicSize * cam.aspect * Mathf.Max(1e-6f, factorX);

        float sx = (_lastSimToWorldScale.x == 0f) ? 1f : _lastSimToWorldScale.x;
        // 1 repeat per "screen view width" when tiling == 1
        return tiling * (sx / screenViewWidthWorld);
    }

    /// <summary>
    /// Triangulates and assigns UVs using TransformedTiling (phase anchored to local mesh so no scrolling on translation).
    /// </summary>
    public void Build()
    {
        _mf ??= GetComponent<MeshFilter>();
        _mr ??= GetComponent<MeshRenderer>();
        _mesh ??= new Mesh { name = "RBMesh2D" };

        var pts = basePointsSim;
        if (pts == null || pts.Count < 3) return;

        int[] tris = new Triangulator(pts.ToArray()).Triangulate();
        if (tris == null || tris.Length < 3) return;

        int n = pts.Count;
        var verts = new Vector3[n];
        var uvs   = new Vector2[n];

        float k = TransformedTiling();
        for (int i = 0; i < n; i++)
        {
            Vector2 p = pts[i];
            verts[i]  = new(p.x, p.y, 0f);  // in sim units; scaled in UpdatePoseWorld
            uvs[i]    = p * k + uvOffset;   // 1 repeat spans GPU view width when tiling == 1
        }

        if (_mesh == null) return;
        _mesh.Clear();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt16;
        _mesh.vertices = verts;
        _mesh.triangles = tris;
        _mesh.uv = uvs;
        _mesh.RecalculateNormals();
        _mesh.RecalculateTangents();
        _mesh.RecalculateBounds();

        _mf.sharedMesh = _mesh;
        if (material)
        {
            _mr.sharedMaterial = material;
            foreach (var prop in material.GetTexturePropertyNames())
            {
                var tex = material.GetTexture(prop);
                if (tex) tex.wrapMode = TextureWrapMode.Repeat; // ensure repetition works
            }
        }

        _lastPtsHash = PointsHash(basePointsSim);
        _lastMat = material;
        _lastTiling = tiling;
        _lastUv = uvOffset;
    }

    /// <summary>
    /// Rotate in sim-space, then scale to world. Cache sim->world scale for TransformedTiling.
    /// </summary>
    public void UpdatePoseWorld(Vector2 worldCenter, float simRotRadians, Vector2 simToWorldScale)
    {
        if (_mesh == null || basePointsSim == null || basePointsSim.Count < 3) return;

        _lastSimToWorldScale = simToWorldScale; // keep UV scale consistent with GPU view width definition

        float c = Mathf.Cos(simRotRadians);
        float s = Mathf.Sin(simRotRadians);

        var verts = _mesh.vertices;
        int n = basePointsSim.Count;
        if (verts == null || verts.Length != n) verts = new Vector3[n];

        for (int i = 0; i < n; i++)
        {
            Vector2 v = basePointsSim[i];
            float rx =  c * v.x - s * v.y;
            float ry =  s * v.x + c * v.y;
            rx *= simToWorldScale.x;
            ry *= simToWorldScale.y;
            verts[i] = new Vector3(rx, ry, 0f);
        }

        _mesh.vertices = verts;
        _mesh.RecalculateBounds();

        var t = transform;
        t.position   = new Vector3(worldCenter.x, worldCenter.y, t.position.z);
        t.rotation   = Quaternion.identity;
        t.localScale = Vector3.one;
    }
}