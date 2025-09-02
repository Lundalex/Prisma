using Unity.Mathematics;
using UnityEngine;

/// <summary>
/// Per-material input that drives atlas packing and shader params.
/// Now uses only colTex, which is sourced from renderMat.bakedTexture (if assigned).
/// </summary>
[System.Serializable]
public class MatInput
{
    // Source for colTex
    public RenderMat renderMat;

    // Shader params
    public float3 baseColor = new(0.0f, 0.0f, 0.0f);
    public float  opacity   = 1.0f;

    // UV transform / tiling: sign of colTexUpScaleFactor toggles mirror repeat (positive = mirror)
    public float2 sampleOffset = new(0, 0);
    public float  colorTextureUpScaleFactor = 1.0f;
    public bool   disableMirrorRepeat = false;

    // Tinting / edge color
    public float3 sampleColorMultiplier = new(1.0f, 1.0f, 1.0f);
    public bool   transparentEdges = false;
    public float3 edgeColor = new (0.0f, 0.0f, 0.0f);
}