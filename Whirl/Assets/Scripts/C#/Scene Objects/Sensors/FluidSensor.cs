using System;
using Resources2;
using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;
using Debug = UnityEngine.Debug;

public class FluidSensor : Sensor
{
    [Header("Primary Customizations")]
    [SerializeField] private FluidSensorType fluidSensorType;

    [Header("Measurement Zone")]
    public Color lineColor;
    public Color areaColor;
    public Rect measurementZone;
    [SerializeField] private float patternModulo;

    [Header("Fluid Sampling")]
    [Range(1, 20), SerializeField] private int SampleSpacing;
    [SerializeField] private bool allowDepthGaps;

    // Private
    private int minX, maxX, minY, maxY;
    private float sampleDensityCorrection;
    private int2 chunksNum;

    int GetChunkKey(int x, int y) => x + y * main.ChunksNum.x;

    private void OnValidate()
    {
        if (PM.Instance.programStarted) InitializeMeasurementParameters();
    }

    public override void InitSensor(Vector2 _)
    {
        UpdatePosition();
        InitializeMeasurementParameters();
    }

    private void InitializeMeasurementParameters()
    {
        if (main == null) return;
        chunksNum = main.ChunksNum;
        float maxInfluenceRadius = main.MaxInfluenceRadius;

        minX = Mathf.Max(Mathf.FloorToInt(measurementZone.min.x / maxInfluenceRadius), 0);
        minY = Mathf.Max(Mathf.FloorToInt(measurementZone.min.y / maxInfluenceRadius), 0);
        maxX = Mathf.Min(Mathf.CeilToInt(measurementZone.max.x / maxInfluenceRadius), chunksNum.x - 1);
        maxY = Mathf.Min(Mathf.CeilToInt(measurementZone.max.y / maxInfluenceRadius), chunksNum.y - 1);

        int numX = ((maxX - minX) / SampleSpacing) + 1;
        int numY = ((maxY - minY) / SampleSpacing) + 1;
        int numberOfIterations = numX * numY;

        sampleDensityCorrection = numberOfIterations > 0
                                  ? (maxX - minX) * (maxY - minY) / (float)numberOfIterations
                                  : 1.0f;
    }

    public SensorArea GetSensorAreaData()
    {
        return new SensorArea
        {
            min = measurementZone.min,
            max = measurementZone.max,
            patternMod = patternModulo,
            lineColor = new float4(Func.ColorToFloat3(lineColor), lineColor.a),
            colorTint = new float4(Func.ColorToFloat3(areaColor), areaColor.a)
        };
    }

    public override void UpdatePosition()
    {
        if (positionType == PositionType.Relative)
        {
            lastJointPos = measurementZone.center;
            Vector2 relativelocalTargetPos = lastJointPos + localTargetPos;
            sensorUI.SetPosition(SimSpaceToCanvasSpace(relativelocalTargetPos));
        }
        else sensorUI.SetPosition(SimSpaceToCanvasSpace(localTargetPos));
    }

    public override void UpdateSensor()
    {
        // Early exit
        if (sensorUI == null) return;
        if (measurementZone.height == 0.0f && measurementZone.width == 0.0f)
        {
            Debug.Log("Measurement zone has either no width or no height. It will not be updated. FluidSensor: " + this.name);
            return;
        }

        // Collect all data contributions and estimate the liquid depth & width
        int totChunkDepthsAllowGaps = 0;
        int totChunkDepthsNoGaps = 0;
        int totChunksWithLiquid = 0;
        float totColumnsWithLiquid = 0;
        float numContributions = 0;
        RecordedFluidData_Translated sumFluidDatas = new();

        for (int x = minX; x <= maxX; x += SampleSpacing)
        {
            if (x < 0 || x >= chunksNum.x) continue;

            bool anyLiquidInColumn = false;
            bool underSurface = true;
            int chunkDepthAllowGaps = 0;
            int chunkDepthNoGaps = 0;
            int potentialChunkDepth = 0;

            for (int y = minY; y <= maxY; y += SampleSpacing)
            {
                if (y < 0 || y >= chunksNum.y) continue;

                int chunkKey = GetChunkKey(x, y);
                potentialChunkDepth++;

                // Retrieve fluid data
                RecordedFluidData_Translated fluidData = new(sensorManager.retrievedFluidDatas[chunkKey], main.FloatIntPrecisionP);
                if (fluidData.numContributions > 0)
                {
                    // Add recorded fluid data
                    AddRecordedFluidData(ref sumFluidDatas, fluidData);
                    numContributions += fluidData.numContributions;

                    if (!anyLiquidInColumn) underSurface = true;
                    anyLiquidInColumn = true;

                    // Liquid depth & volume check
                    totChunksWithLiquid++;
                    chunkDepthAllowGaps = potentialChunkDepth;
                    if (underSurface) chunkDepthNoGaps++;
                }
                else underSurface = false;
            }

            totChunkDepthsAllowGaps += chunkDepthAllowGaps;
            totChunkDepthsNoGaps += chunkDepthNoGaps;
            if (anyLiquidInColumn) totColumnsWithLiquid++;
        }

        // Clamp totColumnsWithLiquid to avoid dividing by 0
        totColumnsWithLiquid = Mathf.Max(totColumnsWithLiquid, 0.1f);

        // Apply sample density correction
        sumFluidDatas.MultiplyAllProperties(sampleDensityCorrection);
        sumFluidDatas.numContributions = numContributions * sampleDensityCorrection;
        sumFluidDatas.totMass *= 0.001f; // g -> kg

        // Final liquid depth, width & volume calculations
        float chunkSize = main.MaxInfluenceRadius * main.SimUnitToMetersFactor;
        float width = totColumnsWithLiquid * SampleSpacing * chunkSize;

        // "Allow Gaps" calculations
        float avgChunkDepthAllowGaps = totChunkDepthsAllowGaps * SampleSpacing / totColumnsWithLiquid;
        float depthAllowGaps = avgChunkDepthAllowGaps * chunkSize;
        float volumeAllowGaps = depthAllowGaps * width * main.ZDepthMeters * 1000;

        // "No Gaps" calculations
        float avgChunkDepthNoGaps = totChunkDepthsNoGaps * SampleSpacing / totColumnsWithLiquid;
        float depthNoGaps = avgChunkDepthNoGaps * chunkSize;
        float volumeNoGaps = depthNoGaps * width * main.ZDepthMeters * 1000;

        // Liquid density is based on the number of liquid chunks
        float density = 1000f * sumFluidDatas.totMass / volumeAllowGaps;

        // Normalize values
        sumFluidDatas.totVelAbs *= main.SimUnitToMetersFactor;
        sumFluidDatas.totVelComponents *= main.SimUnitToMetersFactor;
        sumFluidDatas.totPressure *= main.PressureFactor;

        // Update sensor contents
        UpdateSensorContents(sumFluidDatas, width, depthAllowGaps, depthNoGaps, volumeAllowGaps, volumeNoGaps, density);
    }

    void AddRecordedFluidData(ref RecordedFluidData_Translated a, RecordedFluidData_Translated b)
    {
        a.totTemp += b.totTemp;
        a.totThermalEnergy += b.totThermalEnergy;
        a.totPressure += b.totPressure;
        a.totRigidBodyForces += b.totRigidBodyForces;
        a.totVelComponents += b.totVelComponents;
        a.totVelAbs += b.totVelAbs;
        a.totMass += b.totMass;
    }

    private void UpdateSensorContents(RecordedFluidData_Translated sumFluidDatas, float width, float depthAllowGaps, float depthNoGaps, float volumeAllowGaps, float volumeNoGaps, float density)
    {
        float kineticEnergy = sumFluidDatas.totMass * Mathf.Pow(sumFluidDatas.totVelAbs, 2) / 2f;
        float thermalEnergy = sumFluidDatas.totThermalEnergy;
        float avgTemperature = sumFluidDatas.totTemp / Mathf.Max(sumFluidDatas.numContributions, 0.1f);

        float value = 0f;
        if (sumFluidDatas.numContributions > 0)
        {
            switch (fluidSensorType)
            {
                case FluidSensorType.Mass:
                    value = sumFluidDatas.totMass;
                    break;
                case FluidSensorType.Depth:
                    value = allowDepthGaps ? depthAllowGaps : depthNoGaps;
                    break;
                case FluidSensorType.Volume:
                    value = allowDepthGaps ? volumeAllowGaps : volumeNoGaps;
                    break;
                case FluidSensorType.Density:
                    value = density;
                    break;
                case FluidSensorType.Pressure:
                    value = sumFluidDatas.totPressure / sumFluidDatas.numContributions;
                    break;
                case FluidSensorType.Energy_Total_Kinetic:
                    value = kineticEnergy;
                    break;
                case FluidSensorType.Energy_Total_Thermal:
                    value = thermalEnergy;
                    break;
                case FluidSensorType.Energy_Total_Both:
                    value = kineticEnergy + thermalEnergy;
                    break;
                case FluidSensorType.Energy_Average_Kinetic:
                    value = kineticEnergy / sumFluidDatas.numContributions;
                    break;
                case FluidSensorType.Energy_Average_Thermal:
                    value = thermalEnergy / sumFluidDatas.numContributions;
                    break;
                case FluidSensorType.Energy_Average_Both:
                    value = (kineticEnergy + thermalEnergy) / sumFluidDatas.numContributions;
                    break;
                case FluidSensorType.AverageTemperatureCelcius:
                    value = Utils.KelvinToCelcius(avgTemperature);
                    break;
                case FluidSensorType.AverageTemperatureKelvin:
                    value = avgTemperature;
                    break;
                case FluidSensorType.Velocity_Absolute_Destructive:
                    value = Func.Magnitude(sumFluidDatas.totVelComponents) / sumFluidDatas.numContributions;
                    break;
                case FluidSensorType.Velocity_Absolute_Summative:
                    value = sumFluidDatas.totVelAbs / sumFluidDatas.numContributions;
                    break;
                default:
                    Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                    break;
            }
        }

        // Apply threshold, multiplier, and offset
        if (Mathf.Abs(value * Mathf.Pow(10, 3 * (3 - minPrefixIndex))) < minDisplayValue) value = 0f;
        value *= valueMultiplier;
        value += valueOffset;

        // Set UI
        (string prefix, float displayValue) = GetMagnitudePrefix(value, minPrefixIndex);
        bool isNewUnit = SetSensorUnit(prefix);
        sensorUI.SetMeasurement(displayValue, numDecimals, isNewUnit);
        AddSensorDataToGraph(value);
    }

    public override bool SetSensorUnit(string prefix = "")
    {
        string baseUnit = prefix;
        if (doUseCustomUnit) baseUnit = customUnit;
        else switch (fluidSensorType)
        {
            case FluidSensorType.Mass:
                baseUnit = "kg";
                break;

            case FluidSensorType.Depth:
                baseUnit = "m";
                break;

            case FluidSensorType.Volume:
                baseUnit = "l";
                break;

            case FluidSensorType.Density:
                baseUnit = "kg/m3";
                break;

            case FluidSensorType.Pressure:
                baseUnit = "Pa";
                break;

            case FluidSensorType.Energy_Total_Kinetic:
            case FluidSensorType.Energy_Total_Thermal:
            case FluidSensorType.Energy_Total_Both:
            case FluidSensorType.Energy_Average_Kinetic:
            case FluidSensorType.Energy_Average_Thermal:
            case FluidSensorType.Energy_Average_Both:
                baseUnit = "J";
                break;

            case FluidSensorType.AverageTemperatureCelcius:
                baseUnit = "°C";
                break;

            case FluidSensorType.AverageTemperatureKelvin:
                baseUnit = "°K";
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
            case FluidSensorType.Velocity_Absolute_Summative:
                baseUnit = "m/s";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        string unit = prefix + baseUnit;

        ApplyUnitExceptions(ref prefix, ref unit);

        if (unit != lastUnit)
        {
            sensorUI.SetUnit(baseUnit, unit);
            lastUnit = unit;
            return true;
        }

        return false;
    }

    private void ApplyUnitExceptions(ref string prefix, ref string unit)
    {
        switch (unit)
        {
            case "mkg":
                prefix = "";
                unit = "g";
                break;

            case "μkg":
                prefix = "";
                unit = "mg";
                break;
            
            case "nkg":
                prefix = "";
                unit = "μg";
                break;

            default:
                break;
        }
    }

    public override void SetSensorTitle()
    {
        string title = "Titel här";
        if (doUseCustomTitle) title = customTitle;
        else switch (fluidSensorType)
        {
            case FluidSensorType.Mass:
                title = "Massa";
                break;

            case FluidSensorType.Depth:
                title = "Djup";
                break;

            case FluidSensorType.Volume:
                title = "Volym";
                break;

            case FluidSensorType.Density:
                title = "Densitet";
                break;

            case FluidSensorType.Pressure:
                title = "Tryck";
                break;

            case FluidSensorType.Energy_Total_Kinetic:
            case FluidSensorType.Energy_Average_Kinetic:
                title = "K. Energi";
                break;

            case FluidSensorType.Energy_Total_Thermal:
            case FluidSensorType.Energy_Average_Thermal:
                title = "T. Energi";
                break;

            case FluidSensorType.Energy_Total_Both:
            case FluidSensorType.Energy_Average_Both:
                title = "Energi";
                break;

            case FluidSensorType.AverageTemperatureCelcius:
            case FluidSensorType.AverageTemperatureKelvin:
                title = "Temperatur";
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
            case FluidSensorType.Velocity_Absolute_Summative:
                title = "Hastighet";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.SetTitle(title);
    }

    public override void UpdateSensorTypeDropdown()
    {
        int itemIndex = 0;
        switch (fluidSensorType)
        {
            case FluidSensorType.Mass:
                itemIndex = 0;
                break;

            case FluidSensorType.Depth:
                itemIndex = 1;
                break;

            case FluidSensorType.Volume:
                itemIndex = 2;
                break;

            case FluidSensorType.Density:
                itemIndex = 3;
                break;

            case FluidSensorType.Pressure:
                itemIndex = 4;
                break;

            case FluidSensorType.Energy_Total_Kinetic:
                itemIndex = 5;
                break;

            case FluidSensorType.Energy_Average_Kinetic:
                itemIndex = 6;
                break;

            case FluidSensorType.Energy_Total_Thermal:
                itemIndex = 7;
                break;

            case FluidSensorType.Energy_Average_Thermal:
                itemIndex = 8;
                break;

            case FluidSensorType.Energy_Total_Both:
                itemIndex = 9;
                break;

            case FluidSensorType.Energy_Average_Both:
                itemIndex = 10;
                break;

            case FluidSensorType.AverageTemperatureCelcius:
                itemIndex = 11;
                break;

            case FluidSensorType.AverageTemperatureKelvin:
                itemIndex = 12;
                break;

            case FluidSensorType.Velocity_Absolute_Destructive:
                itemIndex = 13;
                break;

            case FluidSensorType.Velocity_Absolute_Summative:
                itemIndex = 14;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.fluidSensorTypeSelect.selectedItemIndex = itemIndex;
    }

    public void SetFluidSensorType(FluidSensorType fluidSensorType)
    {
        this.fluidSensorType = fluidSensorType;
        SetSensorTitle();
        SetSensorUnit();
    }
}