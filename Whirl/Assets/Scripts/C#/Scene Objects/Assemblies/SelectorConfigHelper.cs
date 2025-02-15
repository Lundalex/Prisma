using UnityEngine;

public class SelectorConfigHelper : Assembly
{
    public int userSelectorIndex;
    
    [Header("References")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private ConfigHelper configHelper;

    // Private static
    [SerializeField] private DataStorage dataStorage;
    
    private void OnEnable()
    {
        ProgramManager.Instance.OnPreStart += AssemblyUpdate;
        RetrieveData();
    }

    private void OnDestroy()
    {
        StoreData();
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
        if (configHelper == null || userSelectorInput == null) return;

        userSelectorInput.SetSelectorIndex(userSelectorIndex);
        configHelper.SetActiveConfigByIndex(userSelectorIndex);
    }
}