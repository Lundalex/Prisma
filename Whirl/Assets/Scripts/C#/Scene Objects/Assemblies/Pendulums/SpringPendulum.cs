using UnityEngine;

public class SpringPendulum : Pendulum
{
    [Header("Pendulum Weight")]
    public float springStiffness = 1000f;
    public float springDamping = 10f;

    [Header("References")]
    [SerializeField] private SceneRigidBody weightObject;

    public override void AssemblyUpdate()
    {
        base.AssemblyUpdate();

        if (weightObject == null)
        {
            Debug.LogWarning("All references are not set. SpringPendulum: " + this.name);
            return;
        }
    }

    public override void SetPendulumData(float length, float mass, float gravity)
    {
        weightObject.transform.localPosition = new Vector3(150, 160 - length);
        weightObject.rbInput.mass = mass;
        weightObject.rbInput.springStiffness = springStiffness;
        weightObject.rbInput.damping = springDamping;
        weightObject.rbInput.gravity = gravity;
    }
}