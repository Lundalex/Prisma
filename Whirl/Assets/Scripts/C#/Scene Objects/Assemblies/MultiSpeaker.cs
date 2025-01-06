using UnityEngine;

public class MultiSpeaker : Assembly
{
    [Header("SpeakerType")]
    public SpeakerType speakerType;

    [Header("Speakers")]
    public float speakerFrequency;
    public float speakerMotorRailLength;

    [Header("References")]
    [SerializeField] private Main main;
    [SerializeField] private SceneRigidBody longSpeaker;
    [SerializeField] private SceneRigidBody onePointSpeaker;
    [SerializeField] private SceneRigidBody twoPointSpeakerA;
    [SerializeField] private SceneRigidBody twoPointSpeakerB;
    [SerializeField] private ConfigHelper configHelper;

    [Header("References - User Inputs")]
    [SerializeField] private UserSelectorInput userSelectorInput;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static SpeakerType storedSpeakerType;
    private static float storedSpeakerFrequency;
    private static float storedSpeakerMotorRailLength;
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
        storedSpeakerType = speakerType;
        storedSpeakerFrequency = speakerFrequency;
        storedSpeakerMotorRailLength = speakerMotorRailLength;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        speakerType = storedSpeakerType;
        speakerFrequency = storedSpeakerFrequency;
        speakerMotorRailLength = storedSpeakerMotorRailLength;
    }

    public override void AssemblyUpdate()
    {
        if (longSpeaker == null || onePointSpeaker == null || twoPointSpeakerA == null || twoPointSpeakerB == null)
        {
            Debug.LogWarning("All references are not set. MultiPendulum: " + this.name);
            return;
        }
        if (main == null || configHelper == null) return;

        // Set speaker data, set the config, and manage the userSliderInput
        bool setSliderActive;
        float lerpSpeed = speakerFrequency / main.ProgramSpeed;
        switch (speakerType)
        {
            case SpeakerType.Long:

                setSliderActive = true;
                longSpeaker.rbInput.lerpSpeed = lerpSpeed;
                configHelper.SetActiveConfigByName("Waves", "Long");
                break;

            case SpeakerType.OnePoint:
                onePointSpeaker.rbInput.lerpSpeed = lerpSpeed;
                onePointSpeaker.rbInput.startPos = onePointSpeaker.rbInput.endPos - new Vector2(speakerMotorRailLength / speakerFrequency, 0);
                setSliderActive = true;
                configHelper.SetActiveConfigByName("Waves", "OnePoint");
                break;

            case SpeakerType.TwoPoints:
                twoPointSpeakerA.rbInput.lerpSpeed = lerpSpeed;
                twoPointSpeakerB.rbInput.lerpSpeed = lerpSpeed;
                twoPointSpeakerA.rbInput.startPos = twoPointSpeakerA.rbInput.endPos - new Vector2(speakerMotorRailLength / speakerFrequency, 0);
                twoPointSpeakerB.rbInput.startPos = twoPointSpeakerB.rbInput.endPos - new Vector2(speakerMotorRailLength / speakerFrequency, 0);
                setSliderActive = true;
                configHelper.SetActiveConfigByName("Waves", "TwoPoints");
                break;

            default:
                setSliderActive = false;
                Debug.LogWarning("SpeakerType '" + speakerType + "' not recognized. MultiSpeaker: " + this.name);
                break;
        }

        UserSliderInput.ActivateSlider(userSliderInput, setSliderActive, speakerFrequency);

        if (userSelectorInput != null) userSelectorInput.SetSelectorIndex((int)speakerType);
    }
}