using Unity.Mathematics;

public struct Mat
{
    // Albedo (RGB) rect in the atlas
    public int2 albedoTexLoc;
    public int2 albedoTexDims;

    // Normal map rect (tangent-space; set x = -1 if unused)
    public int2 normalTexLoc;
    public int2 normalTexDims;

    // ORM rect (Occlusion-Roughness-Metalness in RGB; set x = -1 if unused)
    public int2 ormTexLoc;
    public int2 ormTexDims;

    // UV transform / tiling
    public float2 sampleOffset;
    public float  colTexUpScaleFactor;

    // Material params
    public float3 baseCol;
    public float  opacity;
    public float3 sampleColMul;
    public float3 edgeCol;
};