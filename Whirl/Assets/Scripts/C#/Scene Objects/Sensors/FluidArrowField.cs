using System; 
using Resources2;
using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;
using Debug = UnityEngine.Debug;
using System.Collections.Generic;

public class FluidArrowField : SensorBase
{
    [Header("Primary Customizations")]
    [SerializeField] private FluidArrowFieldType fluidArrowFieldType;
    [SerializeField] private bool doDisplayValueBoxes;

    [Header("Measurement Zone")]
    public bool doRenderMeasurementZone;
    public Color lineColor;
    public Color areaColor;
    public Rect measurementZone;
    [SerializeField] private float patternModulo;

    [Header("Fluid Sampling")]
    [SerializeField, Range(0, 100)] private int sampleSpacing;
    [SerializeField, Range(1, 100)] private int autoScaleReferenceSampleSpacing;
    
    [Header("Smart Interpolation")]
    [SerializeField, Range(0.0f, 180f)] private float rotationLerpSkipThreshold;
    [SerializeField] private float minArrowValue;
    [SerializeField] private float maxArrowValue;
    [SerializeField] private float minArrowLength;
    [SerializeField] private float maxArrowLength;
    [SerializeField] private float minValueChangeForUpdate;
    [SerializeField] private bool doInterpolation = true;
    [SerializeField, Range(1.0f, 100.0f)] private float rotationLerpSpeed = 10f;
    [SerializeField, Range(1.0f, 20.0f)] private float valueLerpSpeed = 10f;

    [Header("Prefab reference")]
    [SerializeField] private GameObject uiArrowPrefab;

    // Dictionaries
    private Dictionary<int, UIArrow> chunkArrows = new();
    private Dictionary<int, float> currentArrowValues = new();
    private Dictionary<int, float> targetArrowRotations = new();
    
    // Private references
    private ArrowManager arrowManager;
    private SensorManager sensorManager;
    private Main main;

    // Other
    private int minX, maxX, minY, maxY;
    private int2 chunksNum;

    private Vector2 canvasResolution;
    private Vector2 boundaryDims = Vector2.zero;

    int GetChunkKey(int x, int y) => x + y * main.ChunksNum.x;

    public void Initialize()
    {
        InitializeMeasurementParameters();
        InitializeArrowField();
        SetConstants();

        PM.Instance.AddFluidArrowField(this);
    }

    public void SetReferences(ArrowManager arrowManager, Main main, SensorManager sensorManager)
    {
        this.arrowManager = arrowManager;
        this.sensorManager = sensorManager;
        this.main = main;
        this.canvasResolution = Func.Int2ToVector2(main.Resolution);
    }

    private void SetConstants()
    {
        
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
    }

    private void InitializeArrowField()
    {
        for (int x = minX; x <= maxX; x += sampleSpacing)
        {
            if (x < 0 || x >= chunksNum.x) continue;

            for (int y = minY; y <= maxY; y += sampleSpacing)
            {
                if (y < 0 || y >= chunksNum.y) continue;

                int chunkKey = GetChunkKey(x, y);

                UIArrow uiArrow = arrowManager.CreateArrow(uiArrowPrefab);
                uiArrow.UpdateArrow(0f, "", 0, 0f);
                uiArrow.SetArrowVisibility(false);

                // Set position
                Vector2 positionSimSpace = new Vector2(x + 0.5f, y + 0.5f) * main.MaxInfluenceRadius;
                Vector2 positionCanvasSpace = SimSpaceToCanvasSpace(positionSimSpace);
                uiArrow.SetPosition(positionCanvasSpace, 0f);

                chunkArrows.Add(chunkKey, uiArrow);
                currentArrowValues.Add(chunkKey, 0f);
                targetArrowRotations.Add(chunkKey, 0f);
            }
        }
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

    public void UpdateScript()
    {
        UpdatePosition();
    }

    public void UpdatePosition()
    {
        foreach (KeyValuePair<int, UIArrow> keyArrowPair in chunkArrows)
        {
            int chunkKey = keyArrowPair.Key;
            UIArrow uiArrow = keyArrowPair.Value;

            (Vector2 currentPos, float currentRotation) = uiArrow.GetPosition();
            // Since arrows are static, position is set to targetPos
            float targetRotation = targetArrowRotations[chunkKey];
            float angleDiff = Mathf.Abs(Mathf.DeltaAngle(currentRotation, targetRotation));

            if (angleDiff > rotationLerpSkipThreshold)
                currentRotation = targetRotation;
            else
                currentRotation = doInterpolation ? Mathf.LerpAngle(currentRotation, targetRotation, PM.Instance.clampedDeltaTime * rotationLerpSpeed) : targetRotation;

            uiArrow.SetPosition(currentRotation);
        }
    }

    public override void UpdateSensor()
    {
        // Early exit
        if (measurementZone.height == 0.0f && measurementZone.width == 0.0f)
        {
            Debug.Log("Measurement zone has either no width or no height. It will not be updated. FluidArrowField: " + this.name);
            return;
        }

        foreach (KeyValuePair<int, UIArrow> keyArrowPair in chunkArrows)
        {
            int chunkKey = keyArrowPair.Key;
            UIArrow uiArrow = keyArrowPair.Value;

            RecordedFluidData_Translated fluidData = new(sensorManager.retrievedFluidDatas[chunkKey], main.FloatIntPrecisionP);
            if (fluidData.numContributions == 0)
            {
                uiArrow.SetArrowVisibility(false);
                continue;
            }

            (Vector2 value, string unit) = GetDisplayInfo(fluidData);

            float targetValueMgn = value.magnitude;
            float currentDisplayedValue = currentArrowValues[chunkKey];
            float newDisplayedValue = doInterpolation ? Mathf.Lerp(currentDisplayedValue, targetValueMgn, PM.Instance.clampedDeltaTime * valueLerpSpeed) : targetValueMgn;
            currentArrowValues[chunkKey] = newDisplayedValue;

            float factor = Mathf.Clamp01((newDisplayedValue - minArrowValue) / Mathf.Max(maxArrowValue - minArrowValue, 0.001f));
            float arrowLength = Mathf.Lerp(minArrowLength, maxArrowLength, factor);
            float scale = sampleSpacing / (float)autoScaleReferenceSampleSpacing;

            float targetRotation = Func.AngleFromDir(value);
            targetArrowRotations[chunkKey] = targetRotation;

            bool forceDisplayBoxInvisibility = fluidArrowFieldType == FluidArrowFieldType.InterParticleForces;
            uiArrow.SetValueBoxVisibility(doDisplayValueBoxes && !forceDisplayBoxInvisibility);

            uiArrow.UpdateArrow(newDisplayedValue, unit, arrowLength, factor, scale);
        }
    }

    private (Vector2 newValue, string unit) GetDisplayInfo(RecordedFluidData_Translated fluidData)
    {
        Vector2 value = Vector2.zero;
        string unit = "";
        switch (fluidArrowFieldType)
        {
            case FluidArrowFieldType.InterParticleForces:
                value = fluidData.totInterParticleAcc;
                unit = "NoUnit";
                break;

            case FluidArrowFieldType.Velocity:
                value = fluidData.numContributions == 0 ? Vector2.zero : fluidData.totVelComponents / fluidData.numContributions;
                unit = "m/s";
                break;

            default:
                Debug.LogWarning("Unrecognised FluidArrowFieldType: " + this.name);
                break;
        }

        return (value, unit);
    }

    private Vector2 SimSpaceToCanvasSpace(Vector2 simCoords)
        => (simCoords / GetBoundaryDims() - new Vector2(0.5f, 0.5f)) * canvasResolution;

    public Vector2 GetBoundaryDims()
    {
        if (boundaryDims == Vector2.zero)
        {
            int2 boundaryDimsInt2 = PM.Instance.main.BoundaryDims;
            boundaryDims = new(boundaryDimsInt2.x, boundaryDimsInt2.y);
        }
        return boundaryDims;
    }
}
