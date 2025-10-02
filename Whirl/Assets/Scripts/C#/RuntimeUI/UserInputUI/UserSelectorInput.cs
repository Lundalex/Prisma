using System.Collections.Generic;
using Michsky.MUIP;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class UserSelectorInput : UserUIElement
{
    [Header("Settings")]
    [SerializeField] private bool disallowAutoModifyField;
    [Range(0.0f, 1000.0f), SerializeField] private float msMaxUpdateFrequency = 100.0f;

    [Header("Start Value")]
    [SerializeField] private int startIndex = 0;

    [Header("Inner Field")]
    public bool useInnerField = false;
    public string innerFieldName;

    [Header("Data Storage")]
    [SerializeField] private bool doUseDataStorage = true;
    [SerializeField] private DataStorage dataStorage;

    [Header("References")]
    [SerializeField] private HorizontalSelector selector;
    [SerializeField] private FieldModifier fieldModifier;

    [Header("Config Activation")]
    public ConfigHelper configHelper;
    public string targetCollectionName = "TargetCollection";

    private int lastValue = -1;
    private Timer updateTimer;
    private bool setupDone = false;

    public int CurrentIndex => selector != null ? selector.index : -1;

    public bool TryGetStoredIndex(out int storedIndex)
    {
        if (doUseDataStorage && dataStorage != null && dataStorage.TryGetValue(out int v))
        {
            storedIndex = v; return true;
        }
        storedIndex = startIndex; return true;
    }

    public void SetSelectorIndex(int index)
    {
        if (selector == null) return;
        if (selector.index == index) return;

        selector.index = selector.defaultIndex = index;
        if (Application.isPlaying && selector.gameObject.activeInHierarchy) selector.UpdateUI();

        lastValue = selector.index;
        if (!disallowAutoModifyField) ModifyField();
        ActivateConfigForIndex(selector.index);

        if (Application.isPlaying && doUseDataStorage && dataStorage != null)
            dataStorage.SetValue(selector.index);
    }

    public override void InitDisplay()
    {
        if (containerTrimImage != null) containerTrimImage.color = primaryColor;
        updateTimer ??= new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.Clamped, true, Func.MsToSeconds(msMaxUpdateFrequency));
        if (title != null) title.text = titleText;
        if (selector != null && !Application.isPlaying)
            lastValue = selector.index;
    }

    private void OnEnable()
    {
        setupDone = false;
        if (selector == null) return;

        if (Application.isPlaying)
        {
            int idx = startIndex;
            if (doUseDataStorage && dataStorage != null)
            {
                if (!dataStorage.TryGetValue(out idx)) // first play or after exit -> seed with start
                {
                    idx = startIndex;
                    dataStorage.SetValue(idx);
                }
            }

            selector.index = selector.defaultIndex = idx;
            selector.UpdateUI();
            lastValue = selector.index;

            if (!disallowAutoModifyField) ModifyField();
            ActivateConfigForIndex(selector.index);
        }
        else
        {
            lastValue = selector.index;
        }
    }

    private void Update()
    {
        if (selector == null) return;

        if (!setupDone)
        {
            if (Application.isPlaying)
            {
                selector.UpdateUI();
                lastValue = selector.index;
            }
            setupDone = true;
        }

        if (selector.index != lastValue)
        {
            updateTimer ??= new Timer(Func.MsToSeconds(msMaxUpdateFrequency), TimeType.Clamped, true, Func.MsToSeconds(msMaxUpdateFrequency));
            if (updateTimer.Check())
            {
                if (!disallowAutoModifyField) ModifyField();
                if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;

                ActivateConfigForIndex(selector.index);
                lastValue = selector.index;
                onValueChanged.Invoke();

                if (Application.isPlaying && doUseDataStorage && dataStorage != null)
                    dataStorage.SetValue(selector.index);
            }
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (fieldModifier == null) return;
        if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, selector.index);
        else               fieldModifier.ModifyField(selector.index);
    }

    private void ActivateConfigForIndex(int index)
    {
        if (configHelper == null) return;
        if (!string.IsNullOrEmpty(targetCollectionName))
            configHelper.SetActiveConfigByNameAndIndex(targetCollectionName, index);
        else
            configHelper.SetActiveConfigByIndex(index);
    }
}