using Unity.Mathematics;

public struct RecordedFluidData
{
    public int totTemp_Int;
    public int totThermalEnergy_Int;
    public int totPressure_Int;
    public int2 totInterParticleAcc_Int2;
    public int2 totVel_Int2;
    public int totVelAbs_Int;
    public int totMass_Int;

    public int numContributions;
};