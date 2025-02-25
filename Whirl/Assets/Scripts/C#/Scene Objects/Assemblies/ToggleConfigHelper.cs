using System.Collections;
using UnityEngine;

public class ToggleConfigHelper : Assembly
{
    public bool userToggleState;
    
    [Header("References")]
    [SerializeField] private UserToggleInput userToggleInput;
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
        dataStorage.SetValue<bool>(userToggleState);
    }

    private void RetrieveData()
    {
        if (!DataStorage.hasValue) return;
        userToggleState = dataStorage.GetValue<bool>();
    }

    public override void AssemblyUpdate()
    {
        if (configHelper == null || userToggleInput == null) return;

        int userToggleIndex = userToggleState ? 1 : 0;
        configHelper.SetActiveConfigByIndex(userToggleIndex);
    }
}