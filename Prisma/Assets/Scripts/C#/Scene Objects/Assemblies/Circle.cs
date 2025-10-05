using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class Circle : Assembly
{

    [Header("Circle Settings")]
    public int numSegments = 16;
    public float radiusFactor = 1.0f;
    public float radius = 50.0f;
    public float referenceRadius = 50.0f;
    public float referenceMass = 1000.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rigidBody;
    [SerializeField] private UserSliderInput userSliderInput;
    [SerializeField] private DataStorage dataStorage;

    private class CircleData {
        public float radius;
        public int numSegments;
        public float referenceRadius;
        public float referenceMass;
    }

    private void OnEnable()
    {
        PM.Instance.OnPreStart += AssemblyUpdate;
        RetrieveData();
    }

    private void OnDestroy()
    {
        StoreData();
        PM.Instance.OnPreStart -= AssemblyUpdate;
    }

    private void StoreData()
    {
        CircleData data = new();
        data.radius = radius;
        data.numSegments = numSegments;
        data.referenceRadius = referenceRadius;
        data.referenceMass = referenceMass;
        dataStorage?.SetValue(data);
    }

    private void RetrieveData()
    {
        CircleData data = dataStorage?.GetValue<CircleData>();
        if (data == null) return;
        radius = data.radius;
        numSegments = data.numSegments;
        referenceRadius = data.referenceRadius;
        referenceMass = data.referenceMass;
    }

    public override void AssemblyUpdate()
    {
        if (Application.isPlaying) RetrieveData();

        if (rigidBody == null)
        {
            Debug.LogWarning($"SceneRigidBody reference must be set in the inspector for Circle: {this.name}");
            return;
        }

        if (numSegments < 3)
        {
            Debug.LogWarning("Cannot create a discrete circle from less than 3 faces. Clamping numSegments to 3");
            numSegments = 3;
        }

        float scaledRadius = radius * radiusFactor;
        Vector2[] circlePoints = GeometryUtils.CenteredCircle(scaledRadius, numSegments);
        rigidBody.OverridePolygonPoints(circlePoints);

        if (referenceRadius > 0)
        {
            float mass = referenceMass * Func.Sqr(scaledRadius / referenceRadius);
            rigidBody.rbInput.mass = mass;
        }
        else
        {
            Debug.LogWarning("Reference Radius must be greater than zero");
        }

        if (userSliderInput != null) userSliderInput.startValue = radius;
    }
}