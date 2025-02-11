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

    [Header("Optimisations")]
    [SerializeField, Range(0f, 1f)] private float minUsagePercentage = 0.5f; 
    [SerializeField] private bool doUseWorkSplitting = false;
    [SerializeField] private int updatesPerFrame = 100;
    
    [Header("Measurement Zone")]
    public bool doRenderMeasurementZone;
    [SerializeField] private Rect measurementZone;
    [SerializeField] private int2 gridOffset;
    [SerializeField] private Color lineColor;
    [SerializeField] private Color areaColor;
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

    private List<int> allChunkKeys = new();
    // Current index into allChunkKeys for updating
    private int currentUpdateIndex = 0;

    // Dictionaries & lists for UIArrows
    private Dictionary<int, UIArrow> chunkArrows = new();
    private Dictionary<int, float> currentArrowValues = new();
    private Dictionary<int, float> targetArrowRotations = new();
    private List<UIArrow> arrowPool = new();

    // Private references
    private ArrowManager arrowManager;
    private SensorManager sensorManager;
    private Main main;

    // Other
    private int minX, maxX, minY, maxY;
    private int2 chunksNum;
    private float compensatedScale;

    private Vector2 canvasResolution;
    private Vector2 boundaryDims = Vector2.zero;
    private float lastSensorUpdateTime;

    int GetChunkKey(int x, int y) => x + y * main.ChunksNum.x;

    public void Initialize()
    {
        chunkArrows.Clear();
        currentArrowValues.Clear();
        targetArrowRotations.Clear();
        allChunkKeys.Clear();

        SetConstants();

        for (int x = minX; x <= maxX; x += sampleSpacing)
        {
            if (x < 0 || x >= chunksNum.x) continue;
            for (int y = minY; y <= maxY; y += sampleSpacing)
            {
                if (y < 0 || y >= chunksNum.y) continue;
                int chunkKey = GetChunkKey(x, y);
                allChunkKeys.Add(chunkKey);
            }
        }

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
        compensatedScale = sampleSpacing / (float)autoScaleReferenceSampleSpacing;
        InitializeMeasurementParameters();
    }

    private void InitializeMeasurementParameters()
    {
        chunksNum = main.ChunksNum;
        float maxInfluenceRadius = main.MaxInfluenceRadius;

        minX = Mathf.Max(Mathf.FloorToInt(measurementZone.min.x / maxInfluenceRadius), 0);
        minY = Mathf.Max(Mathf.FloorToInt(measurementZone.min.y / maxInfluenceRadius), 0);
        maxX = Mathf.Min(Mathf.CeilToInt(measurementZone.max.x / maxInfluenceRadius), chunksNum.x - 1);
        maxY = Mathf.Min(Mathf.CeilToInt(measurementZone.max.y / maxInfluenceRadius), chunksNum.y - 1);

        minX += gridOffset.x;
        minY += gridOffset.y;
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
        foreach (var keyValuePair in chunkArrows)
        {
            int chunkKey = keyValuePair.Key;
            UIArrow uiArrow = keyValuePair.Value;

            float currentRotation = uiArrow.rotation;
            float targetRotation = targetArrowRotations[chunkKey];
            float angleDiff = Func.AbsDeltaAngle(currentRotation, targetRotation);
            currentRotation = angleDiff > rotationLerpSkipThreshold
                                ? targetRotation
                                : Mathf.LerpAngle(currentRotation, targetRotation, PM.Instance.clampedDeltaTime * rotationLerpSpeed);
            
            uiArrow.SetRotation(currentRotation);
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

        float sensorDt = GetDeltaTimeValues();

        if (doUseWorkSplitting)
        {
            // Update a subset of grid cells per frame
            int totalKeys = allChunkKeys.Count;
            if (totalKeys == 0) return;
            int endIndex = currentUpdateIndex + updatesPerFrame;
            for (int i = currentUpdateIndex; i < endIndex; i++)
            {
                int index = i % totalKeys;
                int chunkKey = allChunkKeys[index];
                UpdateArrowForChunk(chunkKey, sensorDt);
            }
            currentUpdateIndex = (currentUpdateIndex + updatesPerFrame) % totalKeys;
        }
        else
        {
            // Iterate over all potential grid cells
            for (int x = minX; x <= maxX; x += sampleSpacing)
            {
                if (x < 0 || x >= chunksNum.x) continue;
                for (int y = minY; y <= maxY; y += sampleSpacing)
                {
                    if (y < 0 || y >= chunksNum.y) continue;
                    int chunkKey = GetChunkKey(x, y);
                    UpdateArrowForChunk(chunkKey, sensorDt);
                }
            }
        }

        CleanUpArrowPool();
    }

    private void UpdateArrowForChunk(int chunkKey, float sensorDt)
    {
        RecordedFluidData_Translated fluidData = new RecordedFluidData_Translated(sensorManager.retrievedFluidDatas[chunkKey], main.FloatIntPrecisionP);
        if (fluidData.numContributions == 0)
        {
            // No contributions; if an arrow exists, return it to the pool
            if (chunkArrows.ContainsKey(chunkKey))
            {
                UIArrow arrow = chunkArrows[chunkKey];
                ReturnArrowToPool(chunkKey, arrow);
            }
            return;
        }

        (Vector2 value, string unit) = GetDisplayInfo(fluidData);

        // Value lerp
        float targetValueMgn = value.magnitude;
        float currentDisplayedValue = currentArrowValues.ContainsKey(chunkKey) ? currentArrowValues[chunkKey] : targetValueMgn;
        float newDisplayedValue = doInterpolation ? Mathf.Lerp(currentDisplayedValue, targetValueMgn, sensorDt * valueLerpSpeed) : targetValueMgn;
        currentArrowValues[chunkKey] = newDisplayedValue;

        // Factor and arrowLength
        float factor = Mathf.Clamp01((newDisplayedValue - minArrowValue) / Mathf.Max(maxArrowValue - minArrowValue, 0.001f));
        if (factor == 0.0f)
        {
            // Arrows with too low values are returned to the pool
            if (chunkArrows.ContainsKey(chunkKey))
            {
                UIArrow arrow = chunkArrows[chunkKey];
                ReturnArrowToPool(chunkKey, arrow);
            }
            return;
        }
        float arrowLength = Mathf.Lerp(minArrowLength, maxArrowLength, factor);

        // Target rotation
        float targetRotation = Func.AngleFromDir(value);
        targetArrowRotations[chunkKey] = targetRotation;

        // Get/create arrow
        if (!chunkArrows.TryGetValue(chunkKey, out UIArrow uiArrow))
        {
            uiArrow = GetArrowFromPool();
            uiArrow.SetScale(compensatedScale);

            int x = chunkKey % main.ChunksNum.x;
            int y = chunkKey / main.ChunksNum.x;
            Vector2 positionSimSpace = new Vector2(x + 0.5f, y + 0.5f) * main.MaxInfluenceRadius;
            Vector2 positionCanvasSpace = SimSpaceToCanvasSpace(positionSimSpace);
            uiArrow.SetCenter(positionCanvasSpace);
            chunkArrows.Add(chunkKey, uiArrow);
        }

        // Value box visibility
        bool forceDisplayBoxInvisibility = fluidArrowFieldType == FluidArrowFieldType.InterParticleForces;
        uiArrow.SetValueBoxVisibility(doDisplayValueBoxes && !forceDisplayBoxInvisibility);

        // Update all arrow data
        uiArrow.UpdateArrow(newDisplayedValue, unit, arrowLength, factor);
    }

    private UIArrow GetArrowFromPool()
    {
        if (arrowPool.Count > 0)
        {
            UIArrow arrow = arrowPool[^1];
            arrowPool.RemoveAt(arrowPool.Count - 1);
            arrow.SetArrowVisibility(true);
            return arrow;
        }
        else
        {
            // Instantiate a new arrow if the pool is empty
            UIArrow newArrow = arrowManager.CreateArrow(uiArrowPrefab);
            newArrow.SetArrowVisibility(true);
            return newArrow;
        }
    }

    private void ReturnArrowToPool(int chunkKey, UIArrow arrow)
    {
        if (chunkArrows.ContainsKey(chunkKey)) chunkArrows.Remove(chunkKey);
        if (currentArrowValues.ContainsKey(chunkKey)) currentArrowValues.Remove(chunkKey);
        if (targetArrowRotations.ContainsKey(chunkKey)) targetArrowRotations.Remove(chunkKey);
        arrow.SetArrowVisibility(false);
        arrowPool.Add(arrow);
    }

    // Remove some arrows if the active arrow ratio is too low
    private void CleanUpArrowPool()
    {
        int activeCount = chunkArrows.Count;
        int totalArrows = activeCount + arrowPool.Count;
        int desiredTotal = Mathf.CeilToInt(activeCount / minUsagePercentage);
        int excess = totalArrows - desiredTotal;
        for (int i = 0; i < excess && arrowPool.Count > 0; i++)
        {
            UIArrow arrow = arrowPool[arrowPool.Count - 1];
            arrowPool.RemoveAt(arrowPool.Count - 1);
            Destroy(arrow.gameObject);
        }
    }
 
    private float GetDeltaTimeValues()
    {
        float sensorDt = PM.Instance.totalScaledTimeElapsed - lastSensorUpdateTime;
        lastSensorUpdateTime = PM.Instance.totalScaledTimeElapsed;
        return sensorDt;
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
            boundaryDims = new Vector2(boundaryDimsInt2.x, boundaryDimsInt2.y);
        }
        return boundaryDims;
    }
}