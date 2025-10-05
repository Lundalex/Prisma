using UnityEngine;

public class SpringVibrator : Assembly
{
    [Header("Vibrator")]
    public float vibratorFrequency = 1.0f;

    [Header("Springs")]
    public float springStiffnessA = 250f;
    public float springStiffnessB = 800f;
    public float springStiffnessC = 2000f;

    [Header("References")]
    [SerializeField] private Main main;
    [SerializeField] private SceneRigidBody vibrator;
    [SerializeField] private SceneRigidBody vibratorHitBox;
    [SerializeField] private SceneRigidBody springObjectA;
    [SerializeField] private SceneRigidBody springObjectB;
    [SerializeField] private SceneRigidBody springObjectC;
    [SerializeField] private UserSliderInput vibratorFrequencyInput;
    [SerializeField] private UserSliderInput springStiffnessInputA;
    [SerializeField] private UserSliderInput springStiffnessInputB;
    [SerializeField] private UserSliderInput springStiffnessInputC;

    // Private static
    private static float storedVibratorFrequency;
    private static float storedSpringStiffnessA;
    private static float storedSpringStiffnessB;
    private static float storedSpringStiffnessC;
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

        storedVibratorFrequency = vibratorFrequency;
        storedSpringStiffnessA = springStiffnessA;
        storedSpringStiffnessB = springStiffnessB;
        storedSpringStiffnessC = springStiffnessC;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        vibratorFrequency = storedVibratorFrequency;
        springStiffnessA = storedSpringStiffnessA;
        springStiffnessB = storedSpringStiffnessB;
        springStiffnessC = storedSpringStiffnessC;
    }

    public override void AssemblyUpdate()
    {
        if (vibrator == null || springObjectA == null || springObjectB == null || springObjectC == null)
        {
            Debug.LogWarning("All references are not set. SpringVibrator: " + this.name);
            return;
        }
        if (main == null) return;

        if (vibratorFrequencyInput != null) vibratorFrequencyInput.startValue = vibratorFrequency;
        if (springStiffnessInputA != null) springStiffnessInputA.startValue = springStiffnessA;
        if (springStiffnessInputB != null) springStiffnessInputB.startValue = springStiffnessB;
        if (springStiffnessInputC != null) springStiffnessInputC.startValue = springStiffnessC;

        float lerpSpeed = vibratorFrequency / main.ProgramSpeed;
        vibrator.rbInput.lerpSpeed = lerpSpeed;
        vibratorHitBox.rbInput.lerpSpeed = lerpSpeed;
        springObjectA.rbInput.springStiffness = springStiffnessA;
        springObjectB.rbInput.springStiffness = springStiffnessB;
        springObjectC.rbInput.springStiffness = springStiffnessC;
    }
}