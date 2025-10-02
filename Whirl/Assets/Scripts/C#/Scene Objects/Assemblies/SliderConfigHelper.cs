using UnityEngine;
using PM = ProgramManager;

public class SliderConfigHelper : Assembly
{
    [Header("References")]
    [SerializeField] private UserSliderInput userSliderInput;
    [SerializeField] private ConfigHelper configHelper;

    private void OnEnable()
    {
        if (PM.Instance != null)
            PM.Instance.OnPreStart += AssemblyUpdate;
        Apply();
    }

    private void OnDisable()
    {
        if (PM.Instance != null)
            PM.Instance.OnPreStart -= AssemblyUpdate;
    }

    public override void AssemblyUpdate() => Apply();

    private void Apply()
    {
        if (configHelper == null || userSliderInput == null) return;

        int idx = Application.isPlaying
            ? Mathf.RoundToInt(userSliderInput.CurrentValue)
            : Mathf.RoundToInt(userSliderInput.TryGetStoredValue(out var v) ? v : userSliderInput.startValue);

        userSliderInput.SetValue(idx);
        configHelper.SetActiveConfigByIndex(idx);
    }
}