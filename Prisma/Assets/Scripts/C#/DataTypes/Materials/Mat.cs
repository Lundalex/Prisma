using Unity.Mathematics;

public struct Mat
{
    // Color (RGB) rect in the atlas
    public int2 colTexLoc;
    public int2 colTexDims;

    // UV transform / tiling
    public float2 sampleOffset;
    public float colTexUpScaleFactor;

    // Material params
    public float3 baseCol;
    public float opacity;
    public float3 sampleColMul;
    public float3 edgeCol;

    // Special effects
    public float edgeRoundingMult;
    public float metallicity;
}