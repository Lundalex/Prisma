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

    [Header("References")]
    [SerializeField] private SwitchManager toggleManager;
    [SerializeField] private FieldModifier fieldModifier;

    // Private
    private bool lastValue;

    public override void InitDisplay()
    {
        if (Application.isPlaying)
        {
            toggleManager.isOn = startValue;
            toggleManager.UpdateUI();
        }
        containerTrimImage.color = primaryColor;
    }

    private void Update()
    {
        if (toggleManager.isOn != lastValue)
        {
            ModifyField();

            PM.Instance.doOnSettingsChanged = true;
            lastValue = toggleManager.isOn;

            onValueChanged.Invoke();
        }

        if (!Application.isPlaying) InitDisplay();
    }

    private void ModifyField()
    {
        if (fieldModifier == null) Debug.LogWarning("FieldModifier not set. UserSliderInput: " + this.name);
        else
        {
            if (useInnerField) fieldModifier.ModifyClassField(innerFieldName, toggleManager.isOn);
            else fieldModifier.ModifyField(toggleManager.isOn);
        }
    }
}