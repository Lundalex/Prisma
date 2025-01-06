using Michsky.MUIP;
using Resources2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;
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

    [Header("References")]
    [SerializeField] private Slider slider;
    [SerializeField] private SliderInput sliderInput;
    [SerializeField] private TMP_InputField sliderInputField;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private float lastValue;
    private Timer updateTimer;

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