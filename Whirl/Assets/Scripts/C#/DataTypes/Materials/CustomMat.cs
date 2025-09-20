using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
using System.IO;
#endif

[CreateAssetMenu(fileName = "CustomMat", menuName = "Rendering/CustomMat")]
public class CustomMat : ScriptableObject
{
    // Name
    public string matName;

    // Shader params
    [ColorUsage(false, true), SerializeField] private Color baseColor = Color.black; // HDR color
    public float baseColorMultiplier = 1.0f;
    [Range(0, 1)] public float opacity = 1.0f;

    // UV transform / tiling
    public float2 sampleOffset = new(0, 0);
    public float colorTextureUpScaleFactor = 1.0f;
    public bool disableMirrorRepeat = false;

    // Tinting / edge color
    [FormerlySerializedAs("sampleColorMultiplier")]
    [ColorUsage(false, true), SerializeField] private Color sampleColor = Color.white; // HDR color (no alpha in UI)
    public float sampleColorMultiplier = 1.0f;
    public bool transparentEdges = false;
    public float3 edgeColor = new(1.0f, 1.0f, 1.0f);

    // Lighting
    [Header("Lighting Multipliers")]
    [Min(0f)] public float edgeRoundingMultiplier = 1.0f;

    // Background flag
    public bool isBackground = false;

    // float3 conversions
    public float3 BaseColor => baseColorMultiplier * GetGlobalMatBrightness() * ToFloat3(baseColor);
    public float3 SampleColor => sampleColorMultiplier * GetGlobalMatBrightness() * ToFloat3(sampleColor);

    // Convenience accessors
    public float EdgeRoundMul      => Mathf.Max(0f, edgeRoundingMultiplier);

    // Persisted content hash to detect inspector changes
    [SerializeField, HideInInspector] private uint _lastHash;

    // --- Helpers ---
    static float3 ToFloat3(Color c)
    {
        // Use linear to keep things physically consistent across color spaces.
        var lc = c.linear;
        return new float3(lc.r, lc.g, lc.b);
    }

    private float GetGlobalMatBrightness()
    {
#if UNITY_EDITOR
        // In edit-time previews, don't depend on scene lookups (avoids spam + inconsistent hashing)
        if (!Application.isPlaying) return 1f;
#endif
        if (isBackground) return 1f;
        var matInput = GameObject.FindGameObjectWithTag("MaterialInput");
        if (!matInput) return 1f;
        var matInputComp = matInput.GetComponent<MaterialInput>();
        if (!matInputComp) return 1f;
        else return matInputComp.globalMatBrightnessMultiplier;
    }

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
        // Use multiplied linear colors so hash reflects actual shader inputs.
        float3 bc = BaseColor;
        float3 sc = SampleColor;

        // Core visual fields
        float4 a = new(bc, opacity);
        float4 b = new(sampleOffset, colorTextureUpScaleFactor, disableMirrorRepeat ? 1f : 0f);
        float4 c = new(sc, transparentEdges ? 1f : 0f);
        float4 d = new(edgeColor, isBackground ? 1f : 0f);

        uint h1 = math.hash(a);
        uint h2 = math.hash(b);
        uint h3 = math.hash(c);
        uint h4 = math.hash(d);

        float2 e = new(EdgeRoundMul);
        uint  h5 = math.hash(e);
        return math.hash(new uint3(
            math.hash(new uint2(
                math.hash(new uint2(h1, h2)),
                math.hash(new uint2(h3, h4))
            )),
            h5,
            0u
        ));
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