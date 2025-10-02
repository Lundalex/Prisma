using System.Collections;
using Michsky.MUIP;
using Resources2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;
using UnityEngine.Events;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class UserSliderInput : UserUIElement
{
    [Header("Helper Texts")]
    [SerializeField] private string leftText;
    [SerializeField] private string unitText;

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
    [SerializeField] private TMP_Text leftTextComp;
    [SerializeField] private TMP_Text unitTextComp;

    private float lastValue;
    private Timer updateTimer;
    private bool isMonitoringValueChange;

    public float CurrentValue => slider != null ? slider.value : startValue;

    public bool TryGetStoredValue(out float stored)
    {
        if (doUseDataStorage && dataStorage != null && dataStorage.TryGetValue(out float v))
        { stored = v; return true; }
        stored = startValue; return true;
    }

    public override void InitDisplay()
    {
        float initVal = startValue;
        if (Application.isPlaying && doUseDataStorage && dataStorage != null)
        {
            if (!dataStorage.TryGetValue(out initVal))
            {
                initVal = startValue;
                dataStorage.SetValue(initVal);
            }
        }

        slider.minValue = minValue;
        slider.maxValue = maxValue;
        sliderInput.decimals = numDecimals;

        slider.value = lastValue = initVal;
        if (sliderInputField != null) sliderInputField.text = StringUtils.FloatToString(initVal, numDecimals);
        if (containerTrimImage != null) containerTrimImage.color = primaryColor;

        updateTimer ??= new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.NonClamped);
        if (title != null) title.text = titleText;
        if (leftTextComp) leftTextComp.text = leftText;
        if (unitTextComp) unitTextComp.text = unitText;
    }

    public void SetValue(float value)
    {
        if (slider == null) return;

        if (Application.isPlaying)
        {
            slider.value = value;
            lastValue = value;
            if (sliderInputField != null) sliderInputField.text = StringUtils.FloatToString(value, numDecimals);

            if (doUseDataStorage && dataStorage != null) dataStorage.SetValue(value);
            ModifyField();
            if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;
            onValueChanged.Invoke();
        }
#if UNITY_EDITOR
        else
        {
            EditorApplication.delayCall += () =>
            {
                startValue = value;
                InitDisplay();
            };
        }
#endif
    }

    private void Update()
    {
        if (slider == null) return;

        if (slider.value != lastValue)
        {
            if (updateTimer != null && updateTimer.Check())
            {
                ModifyField();
                if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;

                lastValue = slider.value;

                if (Application.isPlaying && doUseDataStorage && dataStorage != null)
                    dataStorage.SetValue(lastValue);

                onValueChanged.Invoke();
            }
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (fieldModifier == null) return;

        float value = slider.value;

        if (sliderDataType == SliderDataType.Float)
        {
            if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, value);
            else               fieldModifier.ModifyField(value);
        }
        else
        {
            int iv = (int)value;
            if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, iv);
            else               fieldModifier.ModifyField(iv);
        }
    }

    public static void ActivateSlider(UserSliderInput ui, bool active, float activeValue)
    {
        if (ui == null) return;
        ui.gameObject.SetActive(active);
        if (active) ui.SetValue(activeValue);
    }

    public void StartMonitoringValueChange()
    {
        if (!isMonitoringValueChange && PM.Instance != null && PM.Instance.totalRLTimeSinceSceneLoad > 0.2f)
            StartCoroutine(WaitForMouseRelease());
    }

    private IEnumerator WaitForMouseRelease()
    {
        isMonitoringValueChange = true;
        while (Input.GetMouseButton(0)) yield return null;
        onValueChangeDone.Invoke();
        isMonitoringValueChange = false;
    }

    private void OnEnable() => InitDisplay();
}