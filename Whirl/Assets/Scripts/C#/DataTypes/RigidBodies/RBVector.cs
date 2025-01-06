using Unity.Mathematics;

public struct RBVector
{
    public float2 pos;
    public int parentIndex;
    public RBVector(float2 pos, int parentIndex)
    {
        this.pos = pos;
        this.parentIndex = parentIndex;
    }
};