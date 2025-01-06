using UnityEngine;

public class MultiPendulum : Assembly
{
    [Header("PendulumType")]
    public PendulumType pendulumType;

    [Header("Mathematical / Physical")]
    public float pendulumLength = 30f;
    public float pendulumMass = 1000f;
    public float pendulumGravity = 9.82f;

    [Header("Spring")]
    public float springStiffness = 500f;

    [Header("References")]
    [SerializeField] private MathematicalPendulum mathematical;
    [SerializeField] private PhysicalPendulum physical;
    [SerializeField] private SpringPendulum spring;
    [SerializeField] private ConfigHelper configHelper;

    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static PendulumType storedPendulumType;
    private static float storedPendulumLength;
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
        storedPendulumType = pendulumType;
        storedPendulumLength = pendulumLength;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        pendulumType = storedPendulumType;
        pendulumLength = storedPendulumLength;
    }

    public override void AssemblyUpdate()
    {
        if (mathematical == null || physical == null || spring == null)
        {
            Debug.LogWarning("All references are not set. MultiPendulum: " + this.name);
            return;
        }
        if (configHelper == null) return;

        // Set pendulum data, set the config, and manage the userSliderInput
        bool setSliderActive;
        switch (pendulumType)
        {
            case PendulumType.Mathematical:
                mathematical.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                setSliderActive = true;
                configHelper.SetActiveConfigByName("Pendulums", "Mathematical");
                break;

            case PendulumType.Physical:
                physical.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                setSliderActive = true;
                configHelper.SetActiveConfigByName("Pendulums", "Physical");
                break;

            case PendulumType.Spring:
                spring.SetPendulumData(pendulumLength, pendulumMass, pendulumGravity);
                setSliderActive = true;
                configHelper.SetActiveConfigByName("Pendulums", "Spring");
                break;

            case PendulumType.DoubleMathematical:
                setSliderActive = false;
                configHelper.SetActiveConfigByName("Pendulums", "DoubleMathematical");
                break;

            case PendulumType.DoubleSpring:
                setSliderActive = false;
                configHelper.SetActiveConfigByName("Pendulums", "DoubleSpring");
                break;

            default:
                setSliderActive = false;
                Debug.LogWarning("PendulumType '" + pendulumType + "' not recognized. MultiPendulum: " + this.name);
                break;
        }

        UserSliderInput.ActivateSlider(userSliderInput, setSliderActive, pendulumLength);

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)pendulumType);
    }
}