using Michsky.MUIP;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

[ExecuteInEditMode]
public class UserSelectorInput : UserUIElement
{
    [Header("Settings")]
    [SerializeField] private bool disallowAutoModifyField;
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 100.0f;

    [Header("Inner Field")]
    public bool useInnerField = false;
    public string innerFieldName;

    [Header("Data Storage")]
    [SerializeField] private bool doUseDataStorage;
    [SerializeField] private DataStorage dataStorage;

    [Header("References")]
    [SerializeField] private HorizontalSelector selector;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private int lastValue = -1;
    private Timer updateTimer;
    private bool setupFinnished = false;

    public void SetSelectorIndex(int index)
    {
        selector.index = selector.defaultIndex = lastValue = index;
    }

    public override void InitDisplay()
    {
        if (containerTrimImage != null) containerTrimImage.color = primaryColor;
        updateTimer = new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.Clamped, true, Func.MsToSeconds(msMaxUpdateFrequency));
        lastValue = selector.index;
        if (title != null) title.text = titleText;
    }

    private void Update()
    {
        if (!setupFinnished)
        {
            if (Application.isPlaying) selector.UpdateUI();
            setupFinnished = true;
        }

        if (selector == null) return;
        if (selector.index != lastValue)
        {
            updateTimer ??= new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.Clamped, true, Func.MsToSeconds(msMaxUpdateFrequency));
            if (updateTimer.Check())
            {
                if (!disallowAutoModifyField) ModifyField();

                PM.Instance.doOnSettingsChanged = true;
                lastValue = selector.index;

                onValueChanged.Invoke();
            }
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (selector == null) return;
        if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserSelectorInput: " + this.name);
        else
        {
            if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, selector.index);
            else fieldModifier.ModifyField(selector.index);
        }
    }

    private void OnEnable()
    {
        if (doUseDataStorage && dataStorage != null && Application.isPlaying)
        {
            if (DataStorage.hasValue)
            {
                SetSelectorIndex(dataStorage.GetValue<int>());
            }
            ModifyField();
        }
    }

    private void OnDestroy()
    {
        if (doUseDataStorage && dataStorage != null && Application.isPlaying)
        {
            dataStorage.SetValue(selector.index);
        }
    }
}