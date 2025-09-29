using UnityEngine;

public class SelectorConfigHelper : Assembly
{
    public int userSelectorIndex;
    
    [Header("References")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private ConfigHelper configHelper;

    // Private static
    [SerializeField] private DataStorage dataStorage;

#if UNITY_EDITOR
    private bool _isValidating;
    private void OnValidate()
    {
        _isValidating = true;
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) _isValidating = false;
        };
    }
#endif
    
    private void OnEnable()
    {
        if (ProgramManager.Instance != null)
            ProgramManager.Instance.OnPreStart += AssemblyUpdate;

        RetrieveData();
    }

    private void OnDestroy()
    {
        StoreData();
        if (ProgramManager.Instance != null)
            ProgramManager.Instance.OnPreStart -= AssemblyUpdate;
    }

    private void StoreData()
    {
        dataStorage.SetValue<int>(userSelectorIndex);
    }

    private void RetrieveData()
    {
        if (!DataStorage.hasValue) return;
        userSelectorIndex = dataStorage.GetValue<int>();
    }

    public override void AssemblyUpdate()
    {
        // Skip when not safe to touch UI/Transforms
#if UNITY_EDITOR
        if (_isValidating) return;
        if (!Application.isPlaying) return;
#endif
        if (configHelper == null || userSelectorInput == null) return;

        // Safe at runtime
        userSelectorInput.SetSelectorIndex(userSelectorIndex);
        configHelper.SetActiveConfigByIndex(userSelectorIndex);
    }
}