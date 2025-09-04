using Unity.Mathematics;
using UnityEngine;

public class BaseMat : ScriptableObject
{
    // Shader params
    public float3 baseColor = new(0.0f, 0.0f, 0.0f);
    public float  opacity   = 1.0f;

    // UV transform / tiling: sign of colorTextureUpScaleFactor toggles mirror repeat (positive = mirror)
    public float2 sampleOffset = new(0, 0);
    public float  colorTextureUpScaleFactor = 1.0f;
    public bool   disableMirrorRepeat = false;

    // Tinting / edge color
    public float3 sampleColorMultiplier = new(1.0f, 1.0f, 1.0f);
    public bool   transparentEdges = false;
    public float3 edgeColor = new (0.0f, 0.0f, 0.0f);
}
