using Resources2;
using UnityEngine;

public class PhysicalPendulum : Pendulum
{
    [Header("Position")]
    [SerializeField] private Vector2 position;
    [Header("Pendulum Rod")]
    public float width = 15.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody fixedPoint;
    [SerializeField] private SceneRigidBody rodObject;
    [SerializeField] private SceneRigidBody weightObject;

    // Private static
    private static float storedMass;
    private static float storedGravity;
    private static float storedWidth;
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
        storedWidth = width;
    }

    protected override void RetrieveData()
    {
        base.RetrieveData();
        if (!dataHasBeenStored) return;

        mass = storedMass;
        gravity = storedGravity;
        width = storedWidth;
    }

    public override void AssemblyUpdate()
    {
        base.AssemblyUpdate();

        if (fixedPoint == null || rodObject == null || weightObject == null)
        {
            Debug.LogWarning("All references are not set. PhysicalPendulum: " + this.name);
            return;
        }
    }

    public override void SetPendulumData(float length, float mass, float gravity)
    {
        float halfWidth = width / 2.0f;
        Vector2[] rodMeshPoints = GeometryUtils.Rectangle(halfWidth, -halfWidth - length, -halfWidth, halfWidth);
        
        rodObject.OverridePolygonPoints(rodMeshPoints);
        
        float modifiedLength = length * Const.Sqrt2Div3;
        weightObject.rbInput.localLinkPosOtherRB.y = -modifiedLength;
        weightObject.transform.localPosition = new Vector3(150, 160 - modifiedLength);
        weightObject.rbInput.mass = mass;
        weightObject.rbInput.gravity = gravity;
    }
}