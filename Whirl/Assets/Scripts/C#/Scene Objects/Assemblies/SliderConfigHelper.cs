using UnityEngine;

public class SliderConfigHelper : Assembly
{
    public int userSliderIndex;
    
    [Header("References")]
    [SerializeField] private UserSliderInput userSliderInput;
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
        dataStorage.SetValue<int>(userSliderIndex);
    }

    private void RetrieveData()
    {
        if (!DataStorage.hasValue) return;
        userSliderIndex = dataStorage.GetValue<int>();
    }

    public override void AssemblyUpdate()
    {
        if (configHelper == null || userSliderInput == null) return;

        userSliderInput.startValue = userSliderIndex;
        configHelper.SetActiveConfigByIndex(userSliderIndex);
    }
}