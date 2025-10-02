using UnityEngine;
using PM = ProgramManager;

public class SelectorConfigHelper : Assembly
{
    [Header("References")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private ConfigHelper configHelper;

#if UNITY_EDITOR
    private bool _validating;
    private void OnValidate()
    {
        _validating = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            _validating = false;
            if (!Application.isPlaying) Apply();
        };
    }
#endif

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

    public override void AssemblyUpdate()
    {
#if UNITY_EDITOR
        if (_validating) return;
#endif
        Apply();
    }

    private void Apply()
    {
        if (configHelper == null || userSelectorInput == null) return;

        int idx;
        if (Application.isPlaying && userSelectorInput.CurrentIndex >= 0)
            idx = userSelectorInput.CurrentIndex;
        else if (userSelectorInput.TryGetStoredIndex(out var stored) && stored >= 0)
            idx = stored;
        else
            idx = 0;

        userSelectorInput.SetSelectorIndex(idx);
        configHelper.SetActiveConfigByIndex(idx);
    }
}