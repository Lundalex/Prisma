using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[CreateAssetMenu(fileName = "CustomMat", menuName = "Rendering/CustomMat")]
public class CustomMat : ScriptableObject
{
    // Name (controls the asset file name; also used by TextureBaker for child name)
    public string matName;

    // Shader params
    public float3 baseColor = new(0.0f, 0.0f, 0.0f);
    [Range(0, 1)] public float opacity = 1.0f;

    // UV transform / tiling
    public float2 sampleOffset = new(0, 0);
    public float colorTextureUpScaleFactor = 1.0f;
    public bool disableMirrorRepeat = false;

    // Tinting / edge color
    public float3 sampleColorMultiplier = new(1.0f, 1.0f, 1.0f);
    public bool transparentEdges = false;
    public float3 edgeColor = new(1.0f, 1.0f, 1.0f);

    // Persisted content hash to detect inspector changes
    [SerializeField, HideInInspector] private uint _lastHash;

#if UNITY_EDITOR
    [System.NonSerialized] bool _renameQueued;

    static string SanitizeForAssetName(string s)
    {
        if (string.IsNullOrEmpty(s)) return "CustomMat";
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }

    void QueueRenameIfNeeded()
    {
        if (_renameQueued) return;

        var path = AssetDatabase.GetAssetPath(this);
        if (string.IsNullOrEmpty(path)) return;

        string desired = SanitizeForAssetName(matName);
        string current = Path.GetFileNameWithoutExtension(path);
        if (string.IsNullOrEmpty(desired) || desired == current) return;

        _renameQueued = true;
        EditorApplication.delayCall += () =>
        {
            if (this == null) { _renameQueued = false; return; }
            string p = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(p))
            {
                string cur = Path.GetFileNameWithoutExtension(p);
                if (cur != desired) AssetDatabase.RenameAsset(p, desired);
                matName = name;
                EditorUtility.SetDirty(this);
            }
            _renameQueued = false;
        };
    }
#endif

    private uint ComputeHash()
    {
        float4 a = new(baseColor, opacity);
        float4 b = new(sampleOffset, colorTextureUpScaleFactor, disableMirrorRepeat ? 1f : 0f);
        float4 c = new(sampleColorMultiplier, transparentEdges ? 1f : 0f);
        float4 d = new(edgeColor, 0f);

        uint h1 = math.hash(a);
        uint h2 = math.hash(b);
        uint h3 = math.hash(c);
        uint h4 = math.hash(d);

        return math.hash(new uint4(h1, h2, h3, h4));
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(matName))
        {
            matName = name;
            EditorUtility.SetDirty(this);
        }
#endif
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        if (string.IsNullOrEmpty(matName))
        {
            matName = name;
            EditorUtility.SetDirty(this);
        }
        else if (matName != name) QueueRenameIfNeeded();
#endif
        uint h = ComputeHash();
        if (_lastHash != h)
        {
            _lastHash = h;
            if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;
        }
    }
}