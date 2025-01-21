using Resources2;
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

    [Header("Tilt")]
    public float tilt = 0f; // degrees

    [Header("References")]
    [SerializeField] private Main main;
    [SerializeField] private SceneRigidBody vibratorObject;
    [SerializeField] private SceneRigidBody weightObject;
    [SerializeField] protected UserSliderInput userSliderInput;

    private static float storedFrequency;
    private static float storedMass;
    private static float storedGravity;
    private static float storedLength;
    private static float storedStiffness;
    private static float storedDamping;
    private static float storedTilt;
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
        storedTilt = tilt;
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
        tilt = storedTilt;
    }

    public override void AssemblyUpdate()
    {
        if (vibratorObject == null || weightObject == null) return;
        if (main == null) return;

        if (userSliderInput != null && Application.isPlaying)
            userSliderInput.SetValue(frequency);

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
        Vector2 tiltOffset = Func.RotateDegrees2D(new Vector2(0, length), tilt);
        weightObject.transform.localPosition = new Vector2(150.0f, 40.0f) + tiltOffset;

        // Spring
        weightObject.rbInput.springStiffness = stiffness;
        weightObject.rbInput.damping = damping;   
    }
}
