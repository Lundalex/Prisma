using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;

[CreateAssetMenu(fileName = "CustomMat", menuName = "Rendering/CustomMat")]
public class CustomMat : ScriptableObject
{
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

    // Persisted content hash to detect real inspector changes across domain reloads/reimports
    [SerializeField, HideInInspector] private uint _lastHash;

    private uint ComputeHash()
    {
        // Pack values into float4s, hash each, then combine for stability.
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

    private void OnValidate()
    {
        uint h = ComputeHash();
        if (_lastHash != h)
        {
            _lastHash = h;
            if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;
        }
    }
}