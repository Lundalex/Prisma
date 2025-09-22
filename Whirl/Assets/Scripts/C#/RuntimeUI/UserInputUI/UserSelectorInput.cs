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

    [Header("Internal")]
    [SerializeField] private HorizontalSelector selector;

    [Header("Field Modifier")]
    [SerializeField] private FieldModifier fieldModifier;

    [Header("Config Activation")]
    public ConfigHelper configHelper;
    public string targetCollectionName = "TargetCollection";

    // Private
    private int lastValue = -1;
    private Timer updateTimer;
    private bool setupFinnished = false;

    public void SetSelectorIndex(int index)
    {
        if (selector == null) return;
        if (selector.index == index) return;

        selector.index = selector.defaultIndex = index;
        lastValue = selector.index;

        if (Application.isPlaying && selector.gameObject.activeInHierarchy)
            selector.UpdateUI();

        // Activate matching config by collection+index
        ActivateConfigForIndex(selector.index);

        SaveSelectorValue();
    }

    public override void InitDisplay()
    {
        if (containerTrimImage != null) containerTrimImage.color = primaryColor;

        updateTimer = new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.Clamped, true, Func.MsToSeconds(msMaxUpdateFrequency));
        if (selector != null) lastValue = selector.index;

        if (title != null) title.text = titleText;
    }

    private void OnEnable()
    {
        setupFinnished = false;
        if (selector == null) return;

        if (Application.isPlaying)
        {
            if (doUseDataStorage && dataStorage != null && DataStorage.hasValue)
            {
                // Restore saved index and make that the default so MUIP won’t snap back
                int saved = dataStorage.GetValue<int>();
                selector.index = selector.defaultIndex = saved;
            }

            selector.UpdateUI();
            lastValue = selector.index;

            if (doUseDataStorage && dataStorage != null && !disallowAutoModifyField)
                ModifyField();

            // Ensure config reflects the (possibly restored) current index
            ActivateConfigForIndex(selector.index);
        }
        else
        {
            lastValue = selector.index;
        }
    }

    private void OnDisable()
    {
        setupFinnished = false;
        SaveSelectorValue();
    }

    private void Update()
    {
        if (selector == null) return;

        // One-time refresh in case OnEnable execution order ran before MUIP internals
        if (!setupFinnished)
        {
            if (Application.isPlaying)
            {
                selector.UpdateUI();
                lastValue = selector.index;
            }
            setupFinnished = true;
        }

        if (selector.index != lastValue)
        {
            updateTimer ??= new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.Clamped, true, Func.MsToSeconds(msMaxUpdateFrequency));
            if (updateTimer.Check())
            {
                if (!disallowAutoModifyField) ModifyField();

                if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;

                // Activate config for the new selection index
                ActivateConfigForIndex(selector.index);

                lastValue = selector.index;
                onValueChanged.Invoke();

                // Persist immediately so toggling the object restores the latest value
                SaveSelectorValue();
            }
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (selector == null) return;

        if (fieldModifier == null) return;

        if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, selector.index);
        else               fieldModifier.ModifyField(selector.index);
    }

    private void OnDestroy()
    {
        SaveSelectorValue();
    }

    private void SaveSelectorValue()
    {
        if (!Application.isPlaying || !doUseDataStorage || dataStorage == null || selector == null) return;
        dataStorage.SetValue(selector.index);
    }

    // --- Activate collection config BY INDEX using ConfigHelper API ---
    private void ActivateConfigForIndex(int index)
    {
        if (configHelper == null) return;
        configHelper.SetActiveConfigByNameAndIndex(targetCollectionName, index);
    }
}