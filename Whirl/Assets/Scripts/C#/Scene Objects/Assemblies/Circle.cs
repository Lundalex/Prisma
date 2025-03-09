using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class Circle : Assembly
{

    [Header("Circle Settings")]
    public int numSegments = 16;
    public float radius = 50.0f;
    public float referenceRadius = 50.0f;
    public float referenceMass = 1000.0f;

    [Header("References")]
    [SerializeField] private SceneRigidBody rigidBody;
    [SerializeField] private UserSliderInput userSliderInput;

    // Private static
    private static float storedRadius;
    private static int storedNumSegments;
    private static float storedReferenceRadius;
    private static float storedReferenceMass;
    private static bool dataHasBeenStored = false;

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
        dataHasBeenStored = true;
        storedRadius = radius;
        storedNumSegments = numSegments;
        storedReferenceRadius = referenceRadius;
        storedReferenceMass = referenceMass;
    }

    private void RetrieveData()
    {
        if (!dataHasBeenStored) return;
        radius = storedRadius;
        numSegments = storedNumSegments;
        referenceRadius = storedReferenceRadius;
        referenceMass = storedReferenceMass;
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

        Vector2[] circlePoints = GeometryUtils.CenteredCircle(radius, numSegments);
        rigidBody.OverridePolygonPoints(circlePoints);

        if (referenceRadius > 0)
        {
            float mass = referenceMass * Func.Sqr(radius / referenceRadius);
            rigidBody.rbInput.mass = mass;
        }
        else
        {
            Debug.LogWarning("Reference Radius must be greater than zero");
        }

        if (userSliderInput != null) userSliderInput.startValue = radius;
    }
}