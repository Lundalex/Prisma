using UnityEngine;
using System;
using Unity.Mathematics;
using ChartAndGraph;
using PM = ProgramManager;
using Michsky.MUIP;
using Resources2;

public abstract class Sensor : SensorBase
{
    [Header("Display")]
    [SerializeField] private DataView defaultDataView;
    [Range(1, 2)] public int numDecimals;
    [Range(0, 2)] public int numGraphDecimals;
    [Range(0, 1)] public int numGraphTimeDecimals = 1;
    [Range(0, 3)] public int minPrefixIndex;
    [Range(0.1f, 5.0f)] public float newLowerPrefixThreshold = 0.5f;
    public float minDisplayValue = 0.1f;
    [SerializeField] public Color primaryColor;
    [SerializeField] public bool doUseGradient = true;
    [Range(0.5f, 2.0f)] public float sensorScale = 1;
    public Vector2 localTargetPos;
    public PositionType positionType;
    [NonSerialized] public Vector2 lastJointPos;

    [Header("Value Interpolation")]
    public bool doDisplayValueLerp = true;
    [Range(0.0f, 200.0f)] public float displayValueLerpThreshold = 0.05f;
    public float baseLerpSpeed = 5.0f;
    public float lerpSpeedMultiplier = 10.0f;
    public float minLerpSpeed = 1.0f;
    public float maxLerpSpeed = 20.0f;

    [Header("Overrides")]
    public bool doUseCustomTitle = false;
    public string customTitle = "Title Here";
    public bool doUseCustomUnit = false;
    public string customUnit = "Unit Here";
    public float valueMultiplier = 1.0f;
    public float valueOffset = 0.0f;
    public float graphPositionOffsetX = 0.0f;

    [Header("Settings View")]
    public bool doShowSensorTypeSelector = true;

    [Header("References")]
    [SerializeField] private GameObject sensorUIPrefab;
    [SerializeField] private GameObject dashedRectanglePrefab;
    [SerializeField] private GameObject graphChartPrefab;
    [NonSerialized] public GraphController graphController;
    private Canvas mainCanvas;

    // Private references
    [NonSerialized] public Transform sensorUIContainer;
    [NonSerialized] public Transform sensorOutlineContainer;
    [NonSerialized] public Main main;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public Vector2 canvasResolution;
    [NonSerialized] private GraphChart graphChart;
    [NonSerialized] private ItemLabels itemLabels;
    [NonSerialized] private VerticalAxis verticalAxis;
    [NonSerialized] private HorizontalAxis horizontalAxis;

    // Display
    [NonSerialized] public SensorUI sensorUI;

    // Unit
    [NonSerialized] public string lastUnit = "";
    [NonSerialized] public int lastPrefixIndex = -1;
    [NonSerialized] public Timer newPrefixTimer;

    // Private
    private Vector2 boundaryDims = Vector2.zero;

    public void Initialize(Vector2 sensorUIPos)
    {
        InitSensorUI();
        InitSensor(sensorUIPos);
        graphController.InitGraph(graphChart, itemLabels, verticalAxis, horizontalAxis, numGraphDecimals, numGraphTimeDecimals);
        PM.Instance.sensorManager.SubscribeGraphToCoroutine(graphController);
        
        newPrefixTimer = new Timer(newLowerPrefixThreshold, TimeType.Clamped, true);

        PM.Instance.AddSensor(sensorUI, this);
    }

    public void SetReferences(Transform sensorUIContainer, Transform sensorOutlineContainer, Main main, SensorManager sensorManager)
    {
        this.sensorUIContainer = sensorUIContainer;
        this.sensorOutlineContainer = sensorOutlineContainer;
        this.main = main;
        this.sensorManager = sensorManager;
        this.canvasResolution = Func.Int2ToVector2(main.DefaultResolution);

        graphController = gameObject.GetComponent<GraphController>();
    }

    // Warning: Super unreadable code. However, it's centralized to this function only
    private void InitSensorUI()
    {
        GameObject sensorUIObject = Instantiate(sensorUIPrefab, sensorUIContainer);
        sensorUI = sensorUIObject.GetComponent<SensorUI>();
        bool isStandardResolution = PM.Instance.isStandardResolution;
        if (isStandardResolution && false)
        {
            GameObject sensorUIOutline = Instantiate(dashedRectanglePrefab, sensorOutlineContainer);
            sensorUIOutline.SetActive(false);
            sensorUI.dashedRectangleObject = sensorUIOutline;
            sensorUI.dashedRectangle = sensorUIOutline.GetComponent<DashedRectangle>();
            sensorUI.dashedRectangle.sensorUI = sensorUI;
        }
        GameObject sensorUIGraphChartObject = Instantiate(graphChartPrefab, sensorUI.graphChartContainer);
        Vector3 curPos = sensorUIGraphChartObject.transform.position;
        sensorUIGraphChartObject.transform.position = curPos + new Vector3(graphPositionOffsetX + numGraphDecimals * 2.0f, 0, 0);
        graphChart = sensorUIGraphChartObject.GetComponent<GraphChart>();
        itemLabels = sensorUIGraphChartObject.GetComponent<ItemLabels>();
        verticalAxis = sensorUIGraphChartObject.GetComponent<VerticalAxis>();
        horizontalAxis = sensorUIGraphChartObject.GetComponent<HorizontalAxis>();
        mainCanvas = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<Canvas>();
        sensorUI.swayElementA.mainCanvas = mainCanvas;
        sensorUI.swayElementB.mainCanvas = mainCanvas;
        sensorUI.swayElementC.mainCanvas = mainCanvas;
        sensorUI.swayElementD.mainCanvas = mainCanvas;
        bool isRigidBodySensor = this is RigidBodySensor;
        sensorUI.rigidBodySensorTypeSelectObject.SetActive(isRigidBodySensor && doShowSensorTypeSelector);
        sensorUI.fluidSensorTypeSelectObject.SetActive(!isRigidBodySensor && doShowSensorTypeSelector);
        sensorUI.positionTypeSelector.SetActive(isRigidBodySensor);
        sensorUI.positionTitle.SetActive((!isRigidBodySensor && isStandardResolution) || isRigidBodySensor);
        sensorUI.positionInputFields.SetActive(!isRigidBodySensor && isStandardResolution);
        sensorUI.SetPrimaryColor(primaryColor, doUseGradient);
        sensorUI.sensor = this;
        sensorUI.scaleSlider.value = sensorScale;
        sensorUI.sliderScale = sensorScale;
        sensorUI.SetDataWindow(defaultDataView == DataView.Numeric ? "NumericDisplay" : "GraphDisplay");
        sensorUI.positionTypeSelector.GetComponent<HorizontalSelector>().defaultIndex = positionType == PositionType.Relative ? 0 : 1;
        sensorUI.Initialize();
        SetSensorTitle();
        sensorUIObject.name = "UI - " + this.name;
    }

    public abstract void InitSensor(Vector2 pos);
    public abstract void UpdatePosition();
    public override abstract void UpdateSensor();
    public abstract void UpdateSensorTypeDropdown();
    public abstract void SetSensorTitle();
    public abstract bool SetSensorUnit(string unit = "");

    public (string prefix, float newValue) GetMagnitudePrefix(float value, int minPrefixIndex)
    {
        string[] prefixes = { "n", "μ", "m", "", "k", "M", "G", "T" };
        int prefixIndex = 3; // "" (no prefix) is the default

        string minusPrefix = "";
        if (value < 0)
        {
            value *= -1;
            minusPrefix = "-";
        }

        float originalValue = value * Mathf.Pow(1000, 3 - lastPrefixIndex);

        while (value >= 1000f && prefixIndex < prefixes.Length - 1)
        {
            value /= 1000f;
            prefixIndex++;
        }

        while (value < 1f && prefixIndex > minPrefixIndex)
        {
            value *= 1000f;
            prefixIndex--;
        }

        if (prefixIndex < lastPrefixIndex)
        {
            if (newPrefixTimer.Check())
            {
                lastPrefixIndex = prefixIndex;
                return (minusPrefix + prefixes[prefixIndex], value);
            }
            else return (minusPrefix + prefixes[lastPrefixIndex], originalValue);
        }
        else
        {
            newPrefixTimer.Reset();
            lastPrefixIndex = prefixIndex;
            return (minusPrefix + prefixes[prefixIndex], value);
        }
    }

    public void UpdateScript()
    {
        UpdatePosition();
        if (PM.Instance.programStarted) UpdateSensorTypeDropdown();
    }

    public Vector2 SimSpaceToCanvasSpace(Vector2 simCoords)
        => (simCoords / GetBoundaryDims() - new Vector2(0.5f, 0.5f)) * canvasResolution;

    public Vector2 CanvasSpaceToSimSpace(Vector2 canvasCoords)
        => (canvasCoords / canvasResolution + new Vector2(0.5f, 0.5f)) * GetBoundaryDims();

    public Vector2 GetBoundaryDims()
    {
        if (boundaryDims == Vector2.zero)
        {
            int2 boundaryDimsInt2 = PM.Instance.main.BoundaryDims;
            boundaryDims = new(boundaryDimsInt2.x, boundaryDimsInt2.y);
        }
        return boundaryDims;
    }

    public void AddSensorDataToGraph(float y) => graphController.AddPointsToGraph(new Vector2(PM.Instance.totalScaledTimeElapsed, y));
}