using UnityEngine;

public class MathematicalPendulum : Pendulum
{
    [Header("Pendulum Rod")]
    public float rodWidth = 2.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rodObject;
    [SerializeField] private SceneRigidBody weightObject;

    // Private static
    private static float storedMass;
    private static float storedGravity;
    private static float storedRodWidth;
    private static bool dataHasBeenStored = false;

    protected override void OnEnable()
    {
        base.OnEnable();

        if (isIndependent)
        {
            RetrieveData();
        }
    }

    protected override void OnDestroy()
    {
        if (isIndependent)
        {
            StoreData();
        }
        base.OnDestroy();
    }

    protected override void StoreData()
    {
        base.StoreData();
        dataHasBeenStored = true;

        storedMass = mass;
        storedGravity = gravity;
        storedRodWidth = rodWidth;
    }

    protected override void RetrieveData()
    {
        base.RetrieveData();
        if (!dataHasBeenStored) return;

        mass = storedMass;
        gravity = storedGravity;
        rodWidth = storedRodWidth;
    }

    public override void AssemblyUpdate()
    {
        base.AssemblyUpdate();

        if (rodObject == null || weightObject == null)
        {
            Debug.LogWarning("All references are not set. MathematicalPendulum: " + this.name);
            return;
        }
    }

    public override void SetPendulumData(float length, float mass, float gravity)
    {
        float halfWidth = rodWidth / 2.0f;
        Vector2[] rodMeshPoints = new Vector2[]
        {
            new Vector2(-halfWidth, halfWidth),
            new Vector2(halfWidth, halfWidth),
            new Vector2(halfWidth, -length),
            new Vector2(-halfWidth, -length)
        };
        rodObject.OverridePolygonPoints(rodMeshPoints);

        weightObject.rbInput.localLinkPosOtherRB.y = -length;
        weightObject.transform.localPosition = new Vector3(150, 160 - length);
        weightObject.rbInput.mass = mass;
        weightObject.rbInput.gravity = gravity;
    }
}