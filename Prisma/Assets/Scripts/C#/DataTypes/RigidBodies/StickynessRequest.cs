using Unity.Mathematics;

public struct StickynessRequest
{
    public int pIndex;
    public int StickyLineIndex;
    public float2 StickyLineDst;
    public float absDstToLineSqr;
    public float RBStickyness;
    public float RBStickynessRange;
};