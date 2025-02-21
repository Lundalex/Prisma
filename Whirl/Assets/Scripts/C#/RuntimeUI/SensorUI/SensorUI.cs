using UnityEngine;
using TMPro;
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
    private const float PointerMoveDelay = 0.25f;
    private bool settingsPanelIsClosing = false;

    // Private - Transform fields
    private Vector2 lastPositionFieldValues = Vector2.positiveInfinity;
    private bool positionFieldsHaveBeenModified = false;

    // Private - Scale
    private readonly Vector3 BaseScale = new(0.6f, 0.6f, 0.6f);
    private readonly Vector3 ScaleFactor = new(0.65f, 1.0f, 1.0f);
    private const float SettingsViewActiveFixedScale = 2.0f;
    private const float GraphViewActiveFixedScale = 1.5f;
    private const float MouseDraggingFixedScale = 1.2f;

    // Private - Display value interpolation
    private float currentValue = 0f;
    private float targetValue = 0f;
    private bool isInterpolating = false;

    public void Initialize()
    {
        pointerHoverTimer = new Timer(PointerHoverCooldown, TimeType.Clamped, true, PointerHoverCooldown);
        pointerMoveTimer = new Timer(PointerMoveDelay, TimeType.Clamped, true, 0);
        SetDisplayValue(0, sensor.numDecimals);
    }

    private void Update()
    {
        if (PM.Instance.isAnySensorSettingsViewActive) return;

        bool isTryingToMove = Input.GetMouseButton(0) && !Main.MousePressed.x && !PM.Instance.CheckAnySensorBeingMoved(this);
        if (isTryingToMove)
        {
            if (!pointerMoveTimer.Check(false) || (!pointerHoverArea.CheckIfHovering() && !isBeingMoved)) return;
            isBeingMoved = true;
            OnIsBeingDragged?.Invoke();

            // Get mouse position
            Vector2 mouseSimPos = PM.Instance.main.GetMousePosInSimSpace(true);
            Vector2 newPosition = sensor.SimSpaceToCanvasSpace(mouseSimPos);

            // Set sensor UI position
            rectTransform.localPosition = ClampToScreenBounds(newPosition);

            // Set sensor position
            if (sensor.positionType == PositionType.Relative)
            {
                sensor.localTargetPos = mouseSimPos - sensor.lastJointPos;
            }
            else
            {
                sensor.localTargetPos = mouseSimPos;
            }
            
            if (dashedRectangle != null)
            {
                // Activate dashed rectangle object
                dashedRectangle.SetActive(true);

                // Set dashed rectangle position
                dashedRectangle.SetPosition(TransformUtils.SimSpaceToWorldSpace(mouseSimPos));
                dashedRectangle.SetScale(GetTotalScale() / SettingsViewActiveFixedScale);
            }
        }
        else
        {
            isBeingMoved = false;
            pointerMoveTimer.Reset();

            // Activate dashed rectangle object
            if (dashedRectangle != null) dashedRectangle.SetActive(false);
        }
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
        // Check that the sensor is not a rigid body sensor since that sensor type shouldn't have the option to alter the sensor type
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
        else if (positionFieldsPosition != lastPositionFieldValues)
        {
            positionFieldsHaveBeenModified = true;
        }
    }

    public void OnScaleChanged() => userScale = scaleSlider.value;

    public void OnApplyTransformSettings()
    {
        // Set the sliderScale (a factor of the overall sensor UI scale)
        sliderScale = userScale;

        // Set position from field inputs (only for fluid sensors)
        if (sensor is FluidSensor && positionFieldsHaveBeenModified)
        {
            Vector2 simPos = GetPositionFromInputFields();
            rectTransform.localPosition = ClampToScreenBounds(sensor.SimSpaceToCanvasSpace(simPos));
            sensor.localTargetPos = simPos - sensor.lastJointPos;
        }

        // Reset the sensor graph
        sensor.graphController.ResetGraph();

        // Configure the sensor UI for the newly selected sensor type (if the type has been changed by the user)
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
            sensor.graphController.SetNumGraphDecimals(sensor.numGraphDecimals, sensor.numGraphTimeDecimals);
            if (rigidBodySensorTypeDropdownUsed && sensor is RigidBodySensor rigidBodySensor)
            {
                rigidBodySensor.SetRigidBodySensorType(selectedRigidBodySensorType);
            }
            else if (fluidSensorTypeDropdownUsed && sensor is FluidSensor fluidSensor)
            {
                fluidSensor.SetFluidSensorType(selectedFluidSensorType);
            }
        }
    }

    public void SetPositionType(int newPositionTypeInt)
    {
        // Check that the sensor is not a fluid sensor since that sensor type shouldn't have the option to alter the sensor type
        if (sensor is FluidSensor)
        {
            Debug.LogWarning("Trying to change the sensor UI position type of a fluid sensor. This is not allowed. FluidSensor: " + sensor.name);
            return;
        }

        // Get the new position type
        PositionType newPositionType = (PositionType)newPositionTypeInt;
        if (newPositionType == sensor.positionType) return;

        // Set the new position type
        sensor.positionType = newPositionType;

        // Update the localTargetPos to avoid the sensor UI "teleporting" on screen when the position type is changed
        if (newPositionType == PositionType.Fixed)
        {
            sensor.localTargetPos += sensor.lastJointPos;
        }
        else // newPositionType == PositionType.Relative
        {
            sensor.localTargetPos -= sensor.lastJointPos;
        }
    }
#endregion

#region Set UI position & display values
    public void SetMeasurement(float val, int numDecimals, bool newPrefix)
    {
        if (Mathf.Abs(currentValue - val) <= sensor.displayValueLerpThreshold) return;

        numDecimals = Mathf.Clamp(numDecimals, 1, 2);
        targetValue = Mathf.Clamp(val, -999, 999); // Clamp to max 3 characters

        if (sensor.doDisplayValueLerp && !newPrefix)
        {
            if (!isInterpolating)
            {
                StartCoroutine(LerpMeasurementCoroutine(numDecimals));
            }
        }
        else
        {
            currentValue = targetValue;
            SetDisplayValue(currentValue, numDecimals);
        }
    }

    private IEnumerator LerpMeasurementCoroutine(int numDecimals)
    {
        isInterpolating = true;

        // Interpolate towards targetValue
        while (Mathf.Abs(currentValue - targetValue) > sensor.displayValueLerpThreshold)
        {
            float difference = Mathf.Abs(targetValue - currentValue);
            float currentLerpSpeed = Mathf.Clamp(sensor.baseLerpSpeed + (difference * sensor.lerpSpeedMultiplier), sensor.minLerpSpeed, sensor.maxLerpSpeed);

            currentValue = Mathf.Lerp(currentValue, targetValue, Time.deltaTime * currentLerpSpeed);
            currentValue = Mathf.Clamp(currentValue, -999, 999);

            SetDisplayValue(currentValue, numDecimals);

            yield return null;
        }

        // Set the final value precisely
        currentValue = targetValue;
        SetDisplayValue(currentValue, numDecimals);

        isInterpolating = false;
    }

    private void SetDisplayValue(float val, int numDecimals)
    {
        int integerPart = Mathf.Clamp((int)val, -999, 999); // Clamp to max 3 characters
        int decimalPart = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(val - integerPart) * Mathf.Pow(10, numDecimals)), 0, (int)Mathf.Pow(10, numDecimals) - 1);

        // Update the UI text fields
        integerText.text = integerPart.ToString();
        decimalText.text = decimalPart.ToString($"D{numDecimals}");
    }

    public void SetUnit(string baseUnit, string unit)
    {
        sensor.graphController.SetSuffix(baseUnit);
        unitText.text = unit;
    }

    public void SetTitle(string title) => titleText.text = title;

    public void SetPrimaryColor(Color color)
    {
        decimalText.color = color;
        integerText.color = color;
        containerTrimImage.color = color;
    }

    public void SetPosition(Vector2 uiPos)
    {
        if (((pointerHoverArea.CheckIfHovering() && pointerHoverTimer.Check(false)) || PM.Instance.isAnySensorSettingsViewActive) && !settingsPanelIsClosing)
        {
            uiPos = rectTransform.localPosition;
            isPointerHovering = true;
        }
        else if (isPointerHovering)
        {
            isPointerHovering = false;
            pointerHoverTimer.Reset();
        }
        sensor.graphController.isPointerHovering = isPointerHovering;

        rectTransform.localPosition = ClampToScreenBounds(uiPos);
    }

    public void SetSettingsViewAsEnabled()
    {
        // Invoke the settingsViewStatus program manager event
        OnSettingsViewStatusChanged?.Invoke(true);

        // Make sure the dashed rectangle outline is hidden / showing depending on the sensor type
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
            // Get the sensor UI position
            Vector2 simPos = sensor.CanvasSpaceToSimSpace(rectTransform.localPosition);
            Vector2 worldSpacePos = TransformUtils.SimSpaceToWorldSpace(ClampToScreenBounds(simPos));

            // Compare the current simPos with the last simPos
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

            // Set the dashed rectangle transform
            if (dashedRectangle != null)
            {
                dashedRectangle.SetPosition(worldSpacePos);
                dashedRectangle.SetScale(GetDashedRectangleSettingsViewScale());
            }

            yield return new WaitForSeconds(1 / 30.0f);
        }

        // Set the input field contents
        positionXInput.text = ((int)lastSimPos.x).ToString();
        positionYInput.text = ((int)lastSimPos.y).ToString();
        lastPositionFieldValues = Vector2.positiveInfinity;
        settingsPanelIsClosing = false;
    }

    public void SetSettingsViewAsDisabled()
    {
        // Invoke the settingsViewStatus program manager event
        OnSettingsViewStatusChanged?.Invoke(false);

        // Make sure the dashed rectangle outline is hidden
        if (dashedRectangle != null) dashedRectangle.SetActive(false);
    }

    public void SetDataWindow(string windowName) => dataViewWindowManager.OpenPanel(windowName);
#endregion

#region Other
    private bool CheckIfGraphViewIsActive() => dataViewWindowManager.currentWindowIndex == 0 && settingsViewWindowManager.currentWindowIndex == 0;

    private Vector3 GetDashedRectangleSettingsViewScale()
    {
        return GetTotalScale(true) * 0.95f / SettingsViewActiveFixedScale;
    }

    public Vector3 GetTotalScale(bool settingsViewActive = false)
    {
        float settingsViewFactor = settingsViewActive ? SettingsViewActiveFixedScale : sliderScale;
        float graphViewFactor = CheckIfGraphViewIsActive() ? GraphViewActiveFixedScale : 1;
        float mouseDraggingFactor = isBeingMoved ? MouseDraggingFixedScale * (1 + Mathf.Sin(PM.Instance.totalTimeElapsed * 7.0f) * 0.03f) : 1;

        Vector3 totalScale = settingsViewFactor * graphViewFactor * mouseDraggingFactor * BaseScale;
        return totalScale;
    }

    private Vector2 GetPositionFromInputFields()
    {
        float.TryParse(positionXInput.text, out float positionX);
        float.TryParse(positionYInput.text, out float positionY);

        return new(positionX, positionY);
    }

    private Vector2 ClampToScreenBounds(Vector2 uiPos) => TransformUtils.ClampToScreenBounds(uiPos, outerContainerRectTransform, transform.localScale, ScaleFactor);
#endregion
}