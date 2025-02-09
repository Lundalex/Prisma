using Resources2;
using Unity.Mathematics;
using UnityEngine;
using PM = ProgramManager;

public class FluidArrowField : SensorBase
{
    [Header("Fluid Arrow Field Settings")]
    [SerializeField] private Rect measurementZone;
    [SerializeField] private int SampleSpacing = 1;
    [SerializeField] private float sampleDensity = 1f;
    [SerializeField] private GameObject uiArrowPrefab;
    [SerializeField] private RectTransform arrowContainer;

    [Header("Arrow Display Settings")]
    [SerializeField] private float minArrowLength = 20f;
    [SerializeField] private float maxArrowLength = 100f;
    [SerializeField] private bool doDisplayValueBox = false;

    private UIArrow[,] arrows;
    private int numCellsX, numCellsY;
    private int minX, maxX, minY, maxY;
    private int2 chunksNum;
    private float cellSize;

    private void OnEnable()
    {
        InitializeMeasurementParameters();
        CreateArrowField();
    }

    private void OnValidate()
    {
        if (Application.isPlaying)
        {
            InitializeMeasurementParameters();
            CreateArrowField();
        }
    }

    public void InitSensor(Vector2 _)
    {
        InitializeMeasurementParameters();
        CreateArrowField();
        UpdatePosition();
    }

    private void InitializeMeasurementParameters()
    {
        if (PM.Instance.main == null) return;
        chunksNum = PM.Instance.main.ChunksNum;
        float maxInfluenceRadius = PM.Instance.main.MaxInfluenceRadius;

        minX = Mathf.Max(Mathf.FloorToInt(measurementZone.min.x / maxInfluenceRadius), 0);
        minY = Mathf.Max(Mathf.FloorToInt(measurementZone.min.y / maxInfluenceRadius), 0);
        maxX = Mathf.Min(Mathf.CeilToInt(measurementZone.max.x / maxInfluenceRadius), chunksNum.x - 1);
        maxY = Mathf.Min(Mathf.CeilToInt(measurementZone.max.y / maxInfluenceRadius), chunksNum.y - 1);

        numCellsX = ((maxX - minX) / SampleSpacing) + 1;
        numCellsY = ((maxY - minY) / SampleSpacing) + 1;

        cellSize = maxInfluenceRadius;
    }

    private void CreateArrowField()
    {
        if (arrows != null)
        {
            for (int i = 0; i < arrows.GetLength(0); i++)
            {
                for (int j = 0; j < arrows.GetLength(1); j++)
                {
                    if (arrows[i, j] != null)
                        DestroyImmediate(arrows[i, j].gameObject);
                }
            }
        }

        arrows = new UIArrow[numCellsX, numCellsY];

        for (int i = 0; i < numCellsX; i++)
        {
            for (int j = 0; j < numCellsY; j++)
            {
                GameObject arrowObj = Instantiate(uiArrowPrefab, arrowContainer);
                UIArrow arrow = arrowObj.GetComponent<UIArrow>();
                arrows[i, j] = arrow;
                arrow.SetArrowVisibility(false);
            }
        }
    }

    public void UpdatePosition()
    {
        if (arrowContainer != null)
        {
            Vector2 canvasPos = SimSpaceToCanvasSpace(measurementZone.center);
            arrowContainer.localPosition = canvasPos;
        }
    }

    public override void UpdateSensor()
    {
        if (PM.Instance.main == null ||PM.Instance.sensorManager == null) return;

        int cellIndexX = 0;
        for (int x = minX; x <= maxX; x += SampleSpacing)
        {
            int cellIndexY = 0;
            for (int y = minY; y <= maxY; y += SampleSpacing)
            {
                int chunkKey = x + y * chunksNum.x;
                if (chunkKey < 0 || chunkKey >= PM.Instance.sensorManager.retrievedFluidDatas.Length)
                {
                    cellIndexY++;
                    continue;
                }

                RecordedFluidData_Translated fluidData = new RecordedFluidData_Translated(PM.Instance.sensorManager.retrievedFluidDatas[chunkKey], PM.Instance.main.FloatIntPrecisionP);
                float2 avgVel = float2.zero;
                if (fluidData.numContributions > 0)
                {
                    avgVel = fluidData.totVelComponents / fluidData.numContributions;
                }

                UIArrow arrow = arrows[cellIndexX, cellIndexY];
                Vector2 cellCenterSim = new Vector2((x + 0.5f) * cellSize, (y + 0.5f) * cellSize);
                Vector2 cellCenterCanvas = SimSpaceToCanvasSpace(cellCenterSim);
                float arrowRotation = Func.AngleFromDir(avgVel);
                float velocityMagnitude = math.length(avgVel);
                float factor = Mathf.Clamp01(velocityMagnitude);
                float arrowLength = Mathf.Lerp(minArrowLength, maxArrowLength, factor);

                arrow.SetPosition(cellCenterCanvas, arrowRotation);
                arrow.UpdateArrow(velocityMagnitude, "m/s", arrowLength, factor);
                arrow.SetArrowVisibility(velocityMagnitude > 0);

                cellIndexY++;
            }
            cellIndexX++;
        }
    }

    private Vector2 SimSpaceToCanvasSpace(Vector2 simCoords)
    {
        Vector2 canvasResolution = Func.Int2ToVector2(PM.Instance.main.Resolution);
        Vector2 boundaryDims = new Vector2(PM.Instance.main.ChunksNum.x, PM.Instance.main.ChunksNum.y);
        return (simCoords / boundaryDims - new Vector2(0.5f, 0.5f)) * canvasResolution;
    }
}
