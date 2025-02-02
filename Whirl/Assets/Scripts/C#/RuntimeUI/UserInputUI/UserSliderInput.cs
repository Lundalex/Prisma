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

public class UserSliderInput : UserUIElement
{
    [Header("Settings")]
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 100.0f;
    [Range(0, 2), SerializeField] private int numDecimals;
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
        containerTrimImage.color = primaryColor;
        updateTimer = new Timer(Func.MsToSeconds(msMaxUpdateFrequency));
    }

    public void SetValue(float value)
    {
        startValue = value;
        InitDisplay();
    }

    private void Update()
    {
        if (slider.value != lastValue)
        {
            if (updateTimer.Check())
            {
                ModifyField();

                PM.Instance.doOnSettingsChanged = true;
                lastValue = slider.value;

                onValueChanged.Invoke();
            }
        }
    }

    private void ModifyField()
    {
        if (fieldModifier == null)
        {
            Debug.LogWarning("FieldModifier not set. UserSliderInput: " + this.name);
        }
        else
        {
            if (useInnerField)
                fieldModifier.ModifyClassField(innerFieldName, slider.value);
            else
                fieldModifier.ModifyField(slider.value);
        }
    }

    public static void ActivateSlider(UserSliderInput userSliderInput, bool active, float activeValue)
    {
        if (userSliderInput != null)
        {
            userSliderInput.gameObject.SetActive(active);
            if (active)
            {
                if (Application.isPlaying) userSliderInput.SetValue(activeValue);
                #if UNITY_EDITOR
                    else EditorApplication.delayCall += () => userSliderInput.SetValue(activeValue);
                #endif
            }
        }
    }

    public void StartMonitoringValueChange()
    {
        if (!isMonitoringValueChange && PM.Instance.totalTimeElapsed > 0.5f) StartCoroutine(WaitForMouseRelease());
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
    }

    private void OnDestroy()
    {
        if (doUseDataStorage && dataStorage != null && Application.isPlaying)
        {
            dataStorage.SetValue(slider.value);
        }
    }
}