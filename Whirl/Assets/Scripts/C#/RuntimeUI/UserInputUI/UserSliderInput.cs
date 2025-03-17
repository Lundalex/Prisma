using Michsky.MUIP;
using Resources2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;
using UnityEngine.Events;
using System.Collections;

#if UNITY_EDITOR
    using UnityEditor;
#endif

[ExecuteInEditMode]
public class UserSliderInput : UserUIElement
{
    [Header("Settings")]
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 10.0f;
    [Range(0, 2), SerializeField] private int numDecimals;
    [SerializeField] private SliderDataType sliderDataType;
    public float startValue;
    [SerializeField] private float minValue;
    [SerializeField] private float maxValue;

    [Header("Inner Field")]
    public bool useInnerField = false;
    public string innerFieldName;

    [Header("Data Storage")]
    [SerializeField] private bool doUseDataStorage;
    [SerializeField] private DataStorage dataStorage;

    [Header("Unity Event")]
    public UnityEvent onValueChangeDone;

    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private SliderInput sliderInput;
    [SerializeField] private TMP_InputField sliderInputField;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private float lastValue;
    private Timer updateTimer;
    private bool isMonitoringValueChange;

    public override void InitDisplay()
    {
        slider.value = lastValue = startValue;
        slider.minValue = minValue;
        slider.maxValue = maxValue;
        sliderInput.decimals = numDecimals;
        sliderInputField.text = StringUtils.FloatToString(startValue, 1);
        if (containerTrimImage != null) containerTrimImage.color = primaryColor;
        updateTimer = new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.NonClamped);
        title.text = titleText;
    }

    public void SetValue(float value)
    {
        if (Application.isPlaying)
        {
            startValue = value;
            InitDisplay();
        }
        #if UNITY_EDITOR
        else 
            EditorApplication.delayCall += () => 
            {
                startValue = value;
                InitDisplay();
            };
        #endif
    }

    private void Update()
    {
        if (slider.value != lastValue)
        {
            if (updateTimer != null)
            if (updateTimer.Check())
            {
                ModifyField();

                PM.Instance.doOnSettingsChanged = true;
                lastValue = slider.value;

                onValueChanged.Invoke();
            }
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (fieldModifier == null)
        {
            Debug.LogWarning("FieldModifier not set. UserSliderInput: " + this.name);
        }
        else
        {
            float value = slider.value;
            if (sliderDataType == SliderDataType.Float)
            {
                if (useInnerField)
                    fieldModifier.ModifyClassField(innerFieldName, value);
                else
                    fieldModifier.ModifyField(value);
            }
            else
            {
                if (useInnerField)
                    fieldModifier.ModifyClassField(innerFieldName, (int)value);
                else
                    fieldModifier.ModifyField((int)value);
            }
        }
    }

    public static void ActivateSlider(UserSliderInput userSliderInput, bool active, float activeValue)
    {
        if (userSliderInput != null)
        {
            userSliderInput.gameObject.SetActive(active);
            if (active)
            {
                userSliderInput.SetValue(activeValue);
            }
        }
    }

    public void StartMonitoringValueChange()
    {
        if (!isMonitoringValueChange && PM.Instance.totalRLTimeSinceSceneLoad > 0.2f) StartCoroutine(WaitForMouseRelease());
    }

    private IEnumerator WaitForMouseRelease()
    {
        isMonitoringValueChange = true;

        // Wait until the left mouse button is released
        while (Input.GetMouseButton(0))
        {
            yield return null;
        }

        // Once mouse is no longer held down, invoke the event
        onValueChangeDone.Invoke();

        isMonitoringValueChange = false;
    }

    private void OnEnable()
    {
        if (doUseDataStorage && dataStorage != null && Application.isPlaying)
        {
            if (DataStorage.hasValue) startValue = dataStorage.GetValue<float>();
        }
        if (Application.isPlaying)
            InitDisplay();
    }

    private void OnDestroy()
    {
        if (doUseDataStorage && dataStorage != null && Application.isPlaying)
        {
            dataStorage.SetValue(slider.value);
        }
    }
}