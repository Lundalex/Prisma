using UnityEngine;

public class MultiFluidScene : Assembly
{
    [Header("FluidSceneType")]
    public FluidSceneType fluidSceneType;

    [Header("References")]
    [SerializeField] private ConfigHelper configHelper;
    [SerializeField] private MultiFieldModifier multiFieldModifier;

    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;

    // Private static
    private static FluidSceneType storedFluidSceneType;
    private static bool dataHasBeenStored = false;

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
        dataHasBeenStored = true;
        storedFluidSceneType = fluidSceneType;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        fluidSceneType = storedFluidSceneType;
    }

    public override void AssemblyUpdate()
    {
        if (configHelper == null || multiFieldModifier == null) return;

        // Set the config
        switch (fluidSceneType)
        {
            case FluidSceneType.Intro:
                configHelper.SetActiveConfigByName("Scenes", "Intro");
                break;

            case FluidSceneType.Water:
                configHelper.SetActiveConfigByName("Scenes", "Water");
                break;

            case FluidSceneType.Syrup:
                configHelper.SetActiveConfigByName("Scenes", "Syrup");
                break;

            case FluidSceneType.Slime:
                configHelper.SetActiveConfigByName("Scenes", "Slime");
                break;

            case FluidSceneType.Gel:
                configHelper.SetActiveConfigByName("Scenes", "Gel");
                break;

            default:
                Debug.LogWarning("FluidSceneType '" + fluidSceneType + "' not recognized. MultiFluidScenes: " + this.name);
                break;
        }

        ModifyFields();

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)fluidSceneType);
    }

    private void ModifyFields()
    {
        // 'Intro' scene
        multiFieldModifier.ModifyFieldByFieldName("SubTimeStepsPerRBSimUpdate", fluidSceneType == FluidSceneType.Intro ? 1 : 2);

        // 'Water' scene
        multiFieldModifier.ModifyFieldByFieldName("DoDisplayFluidVelocities", fluidSceneType == FluidSceneType.Water);
        multiFieldModifier.ModifyFieldByFieldName("MaxInteractionRadius", fluidSceneType == FluidSceneType.Water ? 80 : 60);

        // 'Syrup' scene
        int influenceRadius = fluidSceneType == FluidSceneType.Syrup ? 3 : 2;
        multiFieldModifier.ModifyFieldByFieldName("MaxInfluenceRadius", influenceRadius);

        // 'Water' & 'Syrup' scenes
        multiFieldModifier.ModifyFieldByFieldName("DoSimulateParticleSprings", fluidSceneType != FluidSceneType.Water && fluidSceneType != FluidSceneType.Syrup);
    }
}