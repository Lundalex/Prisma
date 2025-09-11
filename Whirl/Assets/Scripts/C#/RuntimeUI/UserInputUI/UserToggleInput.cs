using Michsky.MUIP;
using UnityEngine;
using PM = ProgramManager;

[ExecuteInEditMode]
public class UserToggleInput : UserUIElement
{
    [Header("Settings")]
    [SerializeField] private bool startValue;

    [Header("Inner Field")]
    public bool useInnerField = false;
    public string innerFieldName;

    [Header("Data Storage")]
    [SerializeField] private bool doUseDataStorage;
    [SerializeField] private DataStorage dataStorage;

    [Header("References")]
    [SerializeField] private SwitchManager toggleManager;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private bool lastValue;

    public void SetIsOn(bool on)
    {
        ApplyToggle(on, animate: true);
    }

    public void SetIsOnInstant(bool on)
    {
        ApplyToggle(on, animate: false);
    }

    private void ApplyToggle(bool on, bool animate)
    {
        if (toggleManager == null)
        {
            lastValue = on;
            return;
        }

        if (Application.isPlaying)
        {
            if (animate)
            {
                // Use MUIP's animated paths
                if (on) toggleManager.SetOn();
                else    toggleManager.SetOff();
            }
            else
            {
                // Instant state update (no transition)
                toggleManager.isOn = on;
                toggleManager.UpdateUI();
            }
        }
        else
        {
            // Edit Mode: avoid UpdateUI()/coroutines
            toggleManager.isOn = on;
        }

        // Keep our local mirror in sync so Update() doesn't double-fire immediately
        lastValue = on;
    }

    public override void InitDisplay()
    {
        // Keep Edit Mode safe; only touch MUIP UI paths in Play Mode
        if (Application.isPlaying && toggleManager != null)
        {
            // Initial presentation should be instant; OnEnable (DataStorage) may override this.
            toggleManager.isOn = startValue;
            toggleManager.UpdateUI();
            lastValue = toggleManager.isOn;
        }

        if (containerTrimImage != null) containerTrimImage.color = primaryColor;
    }

    public bool IsOn() => toggleManager != null && toggleManager.isOn;

    private void Update()
    {
        if (toggleManager == null) return;

        // Detect user-driven changes (clicks) or external changes to SwitchManager.isOn
        if (toggleManager.isOn != lastValue)
        {
            ModifyField();

            if (PM.Instance != null) PM.Instance.doOnSettingsChanged = true;
            lastValue = toggleManager.isOn;

            onValueChanged.Invoke();
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (fieldModifier == null)
        {
            Debug.LogWarning("FieldModifier not set. UserToggleInput: " + this.name);
            return;
        }

        if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, toggleManager.isOn);
        else               fieldModifier.ModifyField(toggleManager.isOn);
    }

    private void OnEnable()
    {
        // Only run storage/UI ops when playing to avoid Edit Mode coroutines
        if (Application.isPlaying && doUseDataStorage && dataStorage != null && toggleManager != null)
        {
            if (DataStorage.hasValue)
            {
                // Load stored value INSTANTLY (no startup flash)
                SetIsOnInstant(dataStorage.GetValue<bool>());
            }
            else
            {
                SetIsOnInstant(startValue);
            }

            // Ensure bound field reflects current state on enable
            ModifyField();
        }
    }

    private void OnDestroy()
    {
        if (Application.isPlaying && doUseDataStorage && dataStorage != null && toggleManager != null)
        {
            dataStorage.SetValue(toggleManager.isOn);
        }
    }
}