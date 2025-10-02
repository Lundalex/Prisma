using UnityEngine;
using PM = ProgramManager;

public class AdjustableVelocityRB : Assembly
{
    public enum TargetComponentXY { X, Y }

    [Header("Velocity")]
    [SerializeField] private TargetComponentXY targetComponent;
    public float velocityScaler = 1f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rigidBody;
    [SerializeField] private UserSliderInput userSliderInput;

    private void OnEnable()
    {
        if (PM.Instance != null)
            PM.Instance.OnPreStart += AssemblyUpdate;
        ApplyVelocity();
    }

    private void OnDisable()
    {
        if (PM.Instance != null)
            PM.Instance.OnPreStart -= AssemblyUpdate;
    }

    private void Update() => ApplyVelocity();
    public override void AssemblyUpdate() => ApplyVelocity();

    private void ApplyVelocity()
    {
        if (rigidBody == null || userSliderInput == null) return;

        var v = rigidBody.rbInput.velocity;
        float s = userSliderInput.CurrentValue * velocityScaler;
        if (targetComponent == TargetComponentXY.X) v.x = s;
        else                                        v.y = s;
        rigidBody.rbInput.velocity = v;
    }
}