using TMPro; 
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;
using System;
using PM = ProgramManager;
using Resources2;
using System.Collections;

public class SensorUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private TMP_Text decimalText;
    [SerializeField] private TMP_Text integerText;
    [SerializeField] private TMP_Text unitText;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_InputField positionXInput;
    [SerializeField] private TMP_InputField positionYInput;
    [SerializeField] private Image containerTrimImage;
    [SerializeField] private WindowManager dataViewWindowManager;
    [SerializeField] public WindowManager settingsViewWindowManager;
    [SerializeField] public Slider scaleSlider;
    [SerializeField] public RectTransform rectTransform;
    [SerializeField] public RectTransform outerContainerRectTransform;
    [SerializeField] public DemoElementSway swayElementA;
    [SerializeField] public DemoElementSway swayElementB;
    [SerializeField] public DemoElementSway swayElementC;
    [SerializeField] public DemoElementSway swayElementD;
    [SerializeField] public CustomTwinButtonToggleParent swayParentAB;
    [SerializeField] public CustomTwinButtonToggleParent swayParentCD;
    [SerializeField] public GameObject rigidBodySensorTypeSelectObject;
    [SerializeField] public GameObject fluidSensorTypeSelectObject;
    [SerializeField] public CustomDropdown rigidBodySensorTypeSelect;
    [SerializeField] public CustomDropdown fluidSensorTypeSelect;
    [SerializeField] public PointerHoverArea pointerHoverArea;
    [SerializeField] public Transform graphChartContainer;
    [SerializeField] public GameObject positionTitle;
    [SerializeField] public GameObject positionInputFields;
    [SerializeField] public GameObject positionTypeSelector;
    [SerializeField] public UIGradient outerGradient;
    [SerializeField] public UIGradient trimGradient;
    [SerializeField] public UIGradient innerGradient;

    // Events
    public event Action<bool> OnSettingsViewStatusChanged;
    public event Action OnIsBeingDragged;

    // NonSerialized
    [NonSerialized] public Sensor sensor;
    [NonSerialized] public GameObject dashedRectangleObject;
    [NonSerialized] public DashedRectangle dashedRectangle;
    [NonSerialized] public int sensorIndex;
    [NonSerialized] public float sliderScale;
    [NonSerialized] public float userScale;
    [NonSerialized] public bool isPointerHovering = false;
    [NonSerialized] public bool isBeingMoved = false;

    // Private - Dropdown Select
    private RigidBodySensorType selectedRigidBodySensorType;
    private bool rigidBodySensorTypeDropdownUsed = false;
    private FluidSensorType selectedFluidSensorType;
    private bool fluidSensorTypeDropdownUsed = false;

    // Private - Pointer Hover & Dragging
    private Timer pointerHoverTimer;
    private const float PointerHoverCooldown = 0.25f;

    private Timer pointerMoveTimer;
    private const float PointerMoveDelay = 0.15f;

    private bool dragArmed = false;
    private bool settingsPanelIsClosing = false;

    // Transform fields
    private Vector2 lastPositionFieldValues = Vector2.positiveInfinity;
    private bool positionFieldsHaveBeenModified = false;

    // Scale
    private readonly Vector3 BaseScale = new(0.6f, 0.6f, 0.6f);
    private readonly Vector3 ScaleFactor = new(0.65f, 1.0f, 1.0f);
    private const float SettingsViewActiveFixedScale = 2.0f;
    private const float GraphViewActiveFixedScale = 1.5f;
    private const float MouseDraggingFixedScale = 1.2f;
    private const float HoverFixedScale = 1.10f;

    // Display value interpolation
    private float currentValue = 0f;
    private float targetValue = 0f;
    private bool isInterpolating = false;

    public void Initialize()
    {
        pointerHoverTimer = new Timer(PointerHoverCooldown, TimeType.NonClamped, true, PointerHoverCooldown);
        pointerMoveTimer = new Timer(PointerMoveDelay, TimeType.NonClamped, true, 0);

        SetDisplayValue(0, GetNumDecimalsFromPrecision(sensor.displayPrecision));
    }

    private void Update()
    {
        if (PM.Instance.isAnySensorSettingsViewActive) return;
        if (PM.Instance.fullscreenView != null && PM.Instance.fullscreenView.activeSelf) return;

        if (Main.MousePressed.x)
        {
            if (pointerHoverArea.CheckIfHovering()
                && PM.Instance.HoverMayReact(this)
                && PM.Instance.TryBeginSensorDrag(this))
            {
                dragArmed = true;
                isBeingMoved = false;
                pointerMoveTimer.Reset();
            }
            else
            {
                dragArmed = false;
            }
        }

        if (!dragArmed && Input.GetMouseButton(0)
            && pointerHoverArea.CheckIfHovering()
            && PM.Instance.HoverMayReact(this)
            && PM.Instance.TryBeginSensorDrag(this))
        {
            dragArmed = true;
            isBeingMoved = false;
            pointerMoveTimer.Reset();
        }

        if (Input.GetMouseButtonUp(0))
        {
            Vector2 simPos = sensor.CanvasSpaceToSimSpace(rectTransform.localPosition);
            if (sensor.positionType == PositionType.Relative)
                sensor.localTargetPos = simPos - sensor.lastJointPos;
            else
                sensor.localTargetPos = simPos;

            dragArmed = false;
            isBeingMoved = false;
            pointerMoveTimer.Reset();
            PM.Instance.EndSensorDrag(this);
            if (dashedRectangle != null) dashedRectangle.SetActive(false);
            return;
        }

        bool canMove = Input.GetMouseButton(0)
                       && dragArmed
                       && pointerMoveTimer.Check(false)
                       && !PM.Instance.IsAnotherSensorDragging(this);

        if (canMove)
        {
            isBeingMoved = true;
            OnIsBeingDragged?.Invoke();

            Vector2 mouseSimPos = PM.Instance.main.GetMousePosInSimSpace(true);
            Vector2 newPosition = sensor.SimSpaceToCanvasSpace(mouseSimPos);

            rectTransform.localPosition = ClampToScreenBounds(newPosition);

            if (sensor.positionType == PositionType.Relative)
                sensor.localTargetPos = mouseSimPos - sensor.lastJointPos;
            else
                sensor.localTargetPos = mouseSimPos;

            if (dashedRectangle != null)
            {
                dashedRectangle.SetActive(true);
                dashedRectangle.SetPosition(TransformUtils.SimSpaceToWorldSpace(mouseSimPos));
                dashedRectangle.SetScale(GetTotalScale() / SettingsViewActiveFixedScale);
            }
        }
        else
        {
            isBeingMoved = false;
            if (dashedRectangle != null) dashedRectangle.SetActive(false);
        }
    }

    private void OnDisable()
    {
        if (dragArmed || isBeingMoved)
            PM.Instance.EndSensorDrag(this);

        dragArmed = false;
        isBeingMoved = false;
        isPointerHovering = false;
    }

#region User-triggered functions
    public void OnNewRigidBodySensorType(int rigidBodySensorTypeInt)
    {
        if (sensor is RigidBodySensor)
        {
            selectedRigidBodySensorType = (RigidBodySensorType)rigidBodySensorTypeInt;
            rigidBodySensorTypeDropdownUsed = true;
        }
        else Debug.LogWarning("Mismatch between sensor type and active custom dropdown. SensorUI: " + this.name);
    }

    public void OnNewFluidSensorType(int fluidSensorTypeInt)
    {
        if (sensor is FluidSensor)
        {
            selectedFluidSensorType = (FluidSensorType)fluidSensorTypeInt;
            fluidSensorTypeDropdownUsed = true;
        }
        else Debug.LogWarning("Mismatch between sensor type and active custom dropdown. SensorUI: " + this.name);
    }

    public void OnPositionChanged()
    {
        if (sensor is RigidBodySensor)
        {
            Debug.LogWarning("Trying to change the sensor UI position type of a rigid body sensor. This is not allowed. RigidBodySensor: " + sensor.name);
            return;
        }

        Vector2 positionFieldsPosition = ClampToScreenBounds(GetPositionFromInputFields());
        if (dashedRectangle != null)
        {
            dashedRectangle.SetPosition(TransformUtils.SimSpaceToWorldSpace(positionFieldsPosition));
        }

        if (lastPositionFieldValues.x == Vector2.positiveInfinity.x) lastPositionFieldValues = positionFieldsPosition;
        else if (positionFieldsPosition != lastPositionFieldValues) positionFieldsHaveBeenModified = true;
    }

    public void OnScaleChanged() => userScale = scaleSlider.value;

    public void OnApplyTransformSettings()
    {
        sliderScale = userScale;

        if (sensor is FluidSensor && positionFieldsHaveBeenModified)
        {
            Vector2 simPos = GetPositionFromInputFields();
            rectTransform.localPosition = ClampToScreenBounds(sensor.SimSpaceToCanvasSpace(simPos));
            sensor.localTargetPos = simPos - sensor.lastJointPos;
        }

        if (sensor.doEnableGraph && sensor.graphController != null)
        {
            sensor.graphController.ResetGraph();
        }

        if (rigidBodySensorTypeDropdownUsed || fluidSensorTypeDropdownUsed)
        {
            sensor.doUseCustomTitle = false;
            sensor.valueOffset = 0.0f;
            sensor.valueMultiplier = 1.0f;
            sensor.minPrefixIndex = 2;
            sensor.numGraphDecimals = 1;
            sensor.numGraphTimeDecimals = 0;
            sensor.displayValueLerpThreshold = 0.0f;
            sensor.minDisplayValue = 0.0f;

            if (sensor.doEnableGraph && sensor.graphController != null)
                sensor.graphController.SetNumGraphDecimals(sensor.numGraphDecimals, sensor.numGraphTimeDecimals);

            if (rigidBodySensorTypeDropdownUsed && sensor is RigidBodySensor rigidBodySensor)
                rigidBodySensor.SetRigidBodySensorType(selectedRigidBodySensorType);
            else if (fluidSensorTypeDropdownUsed && sensor is FluidSensor fluidSensor)
                fluidSensor.SetFluidSensorType(selectedFluidSensorType);
        }
    }

    public void SetPositionType(int newPositionTypeInt)
    {
        if (sensor is FluidSensor)
        {
            Debug.LogWarning("Trying to change the sensor UI position type of a fluid sensor. This is not allowed. FluidSensor: " + this.name);
            return;
        }

        PositionType newPositionType = (PositionType)newPositionTypeInt;
        if (newPositionType == sensor.positionType) return;

        sensor.positionType = newPositionType;

        if (newPositionType == PositionType.Fixed)
            sensor.localTargetPos += sensor.lastJointPos;
        else
            sensor.localTargetPos -= sensor.lastJointPos;
    }
#endregion

#region Set UI position & display values
    public void SetMeasurement(float val, int /*ignored*/ numDecimals, bool newPrefix)
    {
        if (Mathf.Abs(currentValue - val) <= sensor.displayValueLerpThreshold) return;

        targetValue = Mathf.Clamp(val, -999, 999);

        if (sensor.doDisplayValueLerp && !newPrefix)
        {
            if (!isInterpolating) StartCoroutine(LerpMeasurementCoroutine());
        }
        else
        {
            currentValue = targetValue;
            SetDisplayValue(currentValue, GetNumDecimalsFromPrecision(sensor.displayPrecision));
        }
    }

    private IEnumerator LerpMeasurementCoroutine()
    {
        isInterpolating = true;

        while (Mathf.Abs(currentValue - targetValue) > sensor.displayValueLerpThreshold)
        {
            float difference = Mathf.Abs(targetValue - currentValue);
            float currentLerpSpeed = Mathf.Clamp(sensor.baseLerpSpeed + (difference * sensor.lerpSpeedMultiplier), sensor.minLerpSpeed, sensor.maxLerpSpeed);

            currentValue = Mathf.Lerp(currentValue, targetValue, Time.deltaTime * currentLerpSpeed);
            currentValue = Mathf.Clamp(currentValue, -999, 999);

            SetDisplayValue(currentValue, GetNumDecimalsFromPrecision(sensor.displayPrecision));
            yield return null;
        }

        currentValue = targetValue;
        SetDisplayValue(currentValue, GetNumDecimalsFromPrecision(sensor.displayPrecision));
        isInterpolating = false;
    }

    private void SetDisplayValue(float val, int decimals)
    {
        bool isNegative = val < 0f;
        float absVal = Mathf.Abs(val);
        float rounded = Quantize(absVal, sensor.displayPrecision);
        float finalVal = isNegative ? -rounded : rounded;

        int maxInteger = 999;
        int integerPart = Mathf.Clamp((int)finalVal, -maxInteger, maxInteger);

        if (decimals > 0)
        {
            int pow = (int)Mathf.Pow(10, decimals);
            int decimalPart = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Abs(finalVal - integerPart) * pow),
                0, pow - 1
            );

            integerText.text = integerPart.ToString();
            decimalText.text = decimalPart.ToString($"D{decimals}");
        }
        else
        {
            integerText.text = integerPart.ToString();
            decimalText.text = "__";
        }
    }

    public void SetUnit(string baseUnit, string unit)
    {
        if (sensor.doEnableGraph && sensor.graphController != null)
            sensor.graphController.SetSuffix(baseUnit);

        unitText.text = unit;
    }

    public void SetTitle(string title) => titleText.text = title;

    public void SetPrimaryColor(Color color, bool doUseGradient)
    {
        decimalText.color = color;
        integerText.color = color;

        containerTrimImage.color = doUseGradient ? Color.white : color;

        outerGradient.enabled = doUseGradient;
        trimGradient.enabled = doUseGradient;
        innerGradient.enabled = true;
    }

    public void SetPosition(Vector2 uiPos)
    {
        bool freezeDueToDrag = isBeingMoved || (dragArmed && Input.GetMouseButton(0));
        bool topHover = pointerHoverArea.CheckIfHovering() && PM.Instance.HoverMayReact(this);

        if (freezeDueToDrag)
        {
            uiPos = rectTransform.localPosition;
        }
        else if (((topHover && pointerHoverTimer.Check(false)) || PM.Instance.isAnySensorSettingsViewActive) && !settingsPanelIsClosing)
        {
            uiPos = rectTransform.localPosition;
            isPointerHovering = true;
        }
        else if (isPointerHovering)
        {
            isPointerHovering = false;
            pointerHoverTimer.Reset();
        }

        if (sensor.doEnableGraph && sensor.graphController != null)
            sensor.graphController.isPointerHovering = isPointerHovering;

        rectTransform.localPosition = ClampToScreenBounds(uiPos);
    }

    public void SetSettingsViewAsEnabled()
    {
        OnSettingsViewStatusChanged?.Invoke(true);

        if (dashedRectangle != null) dashedRectangle.SetActive(sensor is FluidSensor);

        if (sensor is FluidSensor)
        {
            StartCoroutine(SetDashedRectangleTransformCoroutine());
        }
    }

    private IEnumerator SetDashedRectangleTransformCoroutine()
    {
        settingsPanelIsClosing = true;
        Timer timer = new(0.2f);
        Vector2 lastSimPos = Vector2.zero;
        while (!timer.Check())
        {
            Vector2 simPos = sensor.CanvasSpaceToSimSpace(rectTransform.localPosition);
            Vector2 worldSpacePos = TransformUtils.SimSpaceToWorldSpace(ClampToScreenBounds(simPos));

            if (simPos != lastSimPos)
            {
                timer.Reset();
                lastSimPos = simPos;
            }
            else
            {
                yield return new WaitForSeconds(1 / 30.0f);
                continue;
            }

            if (dashedRectangle != null)
            {
                dashedRectangle.SetPosition(worldSpacePos);
                dashedRectangle.SetScale(GetDashedRectangleSettingsViewScale());
            }

            yield return new WaitForSeconds(1 / 30.0f);
        }

        positionXInput.text = ((int)lastSimPos.x).ToString();
        positionYInput.text = ((int)lastSimPos.y).ToString();
        lastPositionFieldValues = Vector2.positiveInfinity;
        settingsPanelIsClosing = false;
    }

    public void SetSettingsViewAsDisabled()
    {
        OnSettingsViewStatusChanged?.Invoke(false);
        if (dashedRectangle != null) dashedRectangle.SetActive(false);
    }

    public void SetDataWindow(string windowName) => dataViewWindowManager.OpenPanel(windowName);
#endregion

#region Other
    private bool CheckIfGraphViewIsActive()
    {
        if (!sensor.doEnableGraph) return false;
        return dataViewWindowManager.currentWindowIndex == 0 && settingsViewWindowManager.currentWindowIndex == 0;
    }

    private Vector3 GetDashedRectangleSettingsViewScale()
    {
        return GetTotalScale(true) * 0.95f / SettingsViewActiveFixedScale;
    }

    public Vector3 GetTotalScale(bool settingsViewActive = false)
    {
        float settingsViewFactor = settingsViewActive ? SettingsViewActiveFixedScale : sliderScale;
        float graphViewFactor = (sensor.doEnableGraph && CheckIfGraphViewIsActive()) ? GraphViewActiveFixedScale : 1;
        float mouseDraggingFactor = isBeingMoved ? MouseDraggingFixedScale * (1 + Mathf.Sin(PM.Instance.totalTimeElapsed * 7.0f) * 0.03f) : 1;
        float hoverFactor = (!PM.Instance.isAnySensorSettingsViewActive && isPointerHovering) ? HoverFixedScale : 1;

        Vector3 totalScale = settingsViewFactor * graphViewFactor * mouseDraggingFactor * hoverFactor * BaseScale;
        return totalScale;
    }

    private Vector2 GetPositionFromInputFields()
    {
        float.TryParse(positionXInput.text, out float positionX);
        float.TryParse(positionYInput.text, out float positionY);
        return new(positionX, positionY);
    }

    private Vector2 ClampToScreenBounds(Vector2 uiPos) =>
        TransformUtils.ClampToScreenBounds(uiPos, outerContainerRectTransform, transform.localScale, ScaleFactor);
#endregion

    // -------- Precision helpers --------

    private static int GetNumDecimalsFromPrecision(DisplayPrecision p) =>
        p switch
        {
            DisplayPrecision.Int_Halfs or DisplayPrecision.Int_Precise => 0,
            DisplayPrecision.OneDec_Halfs or DisplayPrecision.OneDec_Precise => 1,
            DisplayPrecision.TwoDec_Halfs or DisplayPrecision.TwoDec_Precise => 2,
            _ => 0
        };

    private static float Quantize(float v, DisplayPrecision p) =>
        p switch
        {
            DisplayPrecision.Int_Precise      => Mathf.Round(v),
            DisplayPrecision.Int_Halfs        => Mathf.Round(v / 5f) * 5f,
            DisplayPrecision.OneDec_Precise   => Mathf.Round(v * 10f) / 10f,
            DisplayPrecision.OneDec_Halfs     => Mathf.Round(v * 2f) / 2f,
            DisplayPrecision.TwoDec_Precise   => Mathf.Round(v * 100f) / 100f,
            DisplayPrecision.TwoDec_Halfs     => Mathf.Round(v * 20f) / 20f,
            _ => v
        };
}