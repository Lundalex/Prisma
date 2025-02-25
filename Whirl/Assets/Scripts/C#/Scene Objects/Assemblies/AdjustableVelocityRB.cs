using UnityEngine;

public class AdjustableVelocityRB : Assembly
{
    [Header("Velocity")]
    [SerializeField] private TargetComponentXY targetComponent;
    public float velocityScaler;
    public float velocityX;
    public float velocityY;

    [Header("References")]
    [SerializeField] private SceneRigidBody rigidBody;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static float storedVelocityX;
    private static float storedVelocityY;
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
        storedVelocityX = velocityX;
        storedVelocityY = velocityY;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored || !Application.isPlaying) return;
        velocityX = storedVelocityX;
        velocityY = storedVelocityY;
    }

    public override void AssemblyUpdate()
    {
        RetrieveData();

        // Set the initial velocity
        if (rigidBody != null) rigidBody.rbInput.velocity = velocityScaler * new Vector2(velocityX, velocityY);

        if (userSliderInput != null)
        {
            if (targetComponent == TargetComponentXY.X) userSliderInput.SetValue(velocityX);
            else userSliderInput.SetValue(velocityY);
        }
    }
}