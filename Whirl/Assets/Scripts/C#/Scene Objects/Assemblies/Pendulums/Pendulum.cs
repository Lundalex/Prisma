using UnityEngine;

public abstract class Pendulum : Assembly
{
    [Header("Parent")]
    public bool isIndependent;

    [Header("Pendulum")]
    public float pendulumLength = 50f;
    public float mass = 1000f;
    public float gravity = 9.82f;

    [Header("References")]
    [SerializeField] protected UserSliderInput userSliderInput;

    // Private static
    private static float storedPendulumLength;
    private static bool dataHasBeenStored = false;

    protected virtual void OnEnable()
    {
        if (isIndependent)
        {
            ProgramManager.Instance.OnPreStart += AssemblyUpdate;
            RetrieveData();
        }
    }

    protected virtual void OnDestroy()
    {
        if (isIndependent)
        {
            StoreData();
        }
        ProgramManager.Instance.OnPreStart -= AssemblyUpdate;
    }

    protected virtual void StoreData()
    {
        dataHasBeenStored = true;
        storedPendulumLength = pendulumLength;
    }

    protected virtual void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        pendulumLength = storedPendulumLength;
    }

    public override void AssemblyUpdate()
    {
        if (!isIndependent) return;
        if (userSliderInput != null) userSliderInput.startValue = pendulumLength;
        SetPendulumData(pendulumLength, mass, gravity);
    }

    public abstract void SetPendulumData(float length, float mass, float gravity);
}