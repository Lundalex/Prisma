using Unity.Mathematics;

public struct Mat
{
    public int2 colTexLoc;
    public int2 colTexDims;
    public float2 sampleOffset;
    public float colTexUpScaleFactor;
    public float3 baseCol;
    public float opacity;
    public float3 sampleColMul;
    public float3 edgeCol;
};