using UnityEngine;

public class MultiObstacles : Assembly
{
    [Header("ObstaclesType")]
    public ObstaclesType obstaclesType;

    [Header("References")]
    [SerializeField] private ConfigHelper configHelper;

    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;

    // Private static
    private static ObstaclesType storedObstaclesType;
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
        storedObstaclesType = obstaclesType;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        obstaclesType = storedObstaclesType;
    }

    public override void AssemblyUpdate()
    {
        if (configHelper == null) return;

        // Set the config
        switch (obstaclesType)
        {
            case ObstaclesType.None:
                configHelper.SetActiveConfigByName("Obstacles", "None");
                break;

            case ObstaclesType.OneOpening:
                configHelper.SetActiveConfigByName("Obstacles", "OneOpening");
                break;

            case ObstaclesType.TwoOpenings:
                configHelper.SetActiveConfigByName("Obstacles", "TwoOpenings");
                break;

            case ObstaclesType.Mixed:
                configHelper.SetActiveConfigByName("Obstacles", "Mixed");
                break;

            default:
                Debug.LogWarning("ObstaclesType '" + obstaclesType + "' not recognized. MultiObstacles: " + this.name);
                break;
        }

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)obstaclesType);
    }
}