using Unity.Mathematics;

struct RBAdjustment
{
    public int2 deltaPos_Int2;
    public int2 deltaVel_Int2;
    public int deltaRotVel_Int;

    public int2 recordedSpringForce_Int2;
    public int2 recordedFrictionForce_Int2;
}