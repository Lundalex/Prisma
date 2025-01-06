using Resources2;
using Unity.Mathematics;
using UnityEngine;

public struct RecordedFluidData_Translated
{
    public float totTemp;
    public float totThermalEnergy;
    public float totPressure;
    public float2 totVelComponents;
    public float totVelAbs;
    public float totMass;

    public float numContributions;

    public RecordedFluidData_Translated(RecordedFluidData recordedFluidData, float precision)
    {
        // Translate from stored integer values to floating point
        this.totTemp = Func.IntToFloat(recordedFluidData.totTemp_Int, precision);
        this.totThermalEnergy = Func.IntToFloat(recordedFluidData.totThermalEnergy_Int, precision);
        this.totPressure = Func.IntToFloat(recordedFluidData.totPressure_Int, precision);
        this.totVelComponents = Func.Int2ToFloat2(recordedFluidData.totVel_Int2, precision);
        this.totVelAbs = Func.IntToFloat(recordedFluidData.totVelAbs_Int, precision);
        this.totMass = Func.IntToFloat(recordedFluidData.totMass_Int, precision);
        this.numContributions = Mathf.Round(recordedFluidData.numContributions);
    }

    public void MultiplyAllProperties(float factor)
    {
        this.totTemp *= factor;
        this.totThermalEnergy *= factor;
        this.totPressure *= factor;
        this.totVelComponents *= factor;
        this.totVelAbs *= factor;
        this.totMass *= factor;
    }
};