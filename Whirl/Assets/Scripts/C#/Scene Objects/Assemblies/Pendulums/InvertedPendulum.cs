using UnityEngine;

public class InvertedPendulum : Assembly
{
    [Header("Vibrator")]
    public float frequency = 10.0f;

    [Header("Weight")]
    public float mass = 1000.0f;
    public float gravity = 9.82f;

    [Header("Spring")]
    public float length = 70.0f;
    public float stiffness = 20000.0f;
    public float damping = 10000.0f;

    [Header("References")]
    [SerializeField] private Main main;
    [SerializeField] private SceneRigidBody vibratorObject;
    [SerializeField] private SceneRigidBody weightObject;
    [SerializeField] protected UserSliderInput userSliderInput;

    // Private static
    private static float storedFrequency;
    private static float storedMass;
    private static float storedGravity;
    private static float storedLength;
    private static float storedStiffness;
    private static float storedDamping;
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

        storedFrequency = frequency;
        storedMass = mass;
        storedGravity = gravity;
        storedLength = length;
        storedStiffness = stiffness;
        storedDamping = damping;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;

        frequency = storedFrequency;
        mass = storedMass;
        gravity = storedGravity;
        length = storedLength;
        stiffness = storedStiffness;
        damping = storedDamping;
    }

    public override void AssemblyUpdate()
    {
        if (vibratorObject == null || weightObject == null)
        {
            Debug.LogWarning("All references are not set. InvertedPendulum: " + this.name);
            return;
        }
        if (main == null) return;

        if (userSliderInput != null && Application.isPlaying) userSliderInput.SetValue(frequency);

        SetPendulumData();
    }

    private void SetPendulumData()
    {
        // Vibrator
        float lerpSpeed = frequency / main.ProgramSpeed;
        vibratorObject.rbInput.lerpSpeed = lerpSpeed;

        // Weight
        weightObject.rbInput.mass = mass;
        weightObject.rbInput.gravity = gravity;
        weightObject.transform.localPosition = new(150.0f, 40.0f + length);

        // Spring
        weightObject.rbInput.springStiffness = stiffness;
        weightObject.rbInput.damping = damping;   
    }
}
