using UnityEngine;
using Resources2;
using PM = ProgramManager;
public class SimpleMechanicalSwitch : Assembly
{
    [Header("Movability")]
    [SerializeField] private Axis2D axis;
    public float minOffset;
    public float maxOffset;

    [Header("Collider")]
    public ColliderType colliderType;
    public Vector2 position;
    public float width;
    public float height;
    public float mass;

    [Header("Rail")]
    public float railPadding;
    public bool doRenderRail;

    [Header("References")]
    [SerializeField] private SceneRigidBody borderCollider;
    [SerializeField] private SceneRigidBody sceneCollider;
    [SerializeField] private SceneRigidBody railVisualization;
    private Main main;

    private void OnEnable()
    {
        PM.Instance.OnPreStart += AssemblyUpdate;
    }

    private void OnDestroy()
    {
        PM.Instance.OnPreStart -= AssemblyUpdate;
    }

    public override void AssemblyUpdate()
    {
        if (borderCollider == null || sceneCollider == null || railVisualization == null)
        {
            Debug.LogWarning("All references are not set. MechanicalSwitch: " + this.name);
            return;
        }
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera")?.GetComponent<Main>();
        if (main == null) return;

        // Offset values
        Vector2 xOffset = Vector2.zero;
        Vector2 yOffset = Vector2.zero;
        if (axis == Axis2D.X)
        {
            xOffset = new(minOffset, maxOffset);
        }
        else // axis == Axis2D.Y
        {
            yOffset = new(minOffset, maxOffset);
        }

        // Boundary dimensions
        Vector2 boundaryDims = Utils.Int2ToVector2(main.BoundaryDims);

        // Border collider center position
        Vector2 borderBoundaryCenter;
        borderBoundaryCenter.x = Func.Avg(xOffset.x, boundaryDims.x - xOffset.y);
        borderBoundaryCenter.y = Func.Avg(boundaryDims.y - yOffset.y, yOffset.x);

        // Border colliser mesh points
        Vector2[] borderColliderPoints = GeometryUtils.Rectangle(boundaryDims.y - yOffset.y - main.RigidBodyPadding, yOffset.x + main.RigidBodyPadding, xOffset.x + main.RigidBodyPadding, boundaryDims.x - xOffset.y - main.RigidBodyPadding);
        borderCollider.OverridePolygonPoints(borderColliderPoints);
        borderCollider.gameObject.transform.position = Vector3.zero;

        // Scene collider mesh points
        Vector2[] sceneColliderPoints = GeometryUtils.CenteredRectangle(width, height);
        sceneCollider.OverridePolygonPoints(sceneColliderPoints);
        sceneCollider.gameObject.transform.position = position;
        sceneCollider.rbInput.localLinkPosOtherRB = position - borderBoundaryCenter;

        // Rail visualization mesh points
        Vector2 railSize;
        Vector2 offset;
        if (axis == Axis2D.X)
        {
            railSize = new(width + minOffset + maxOffset, height);
            offset = new((maxOffset - minOffset) * 0.5f, 0f);
        }
        else // axis == Axis2D.Y
        {
            railSize = new(width, height + minOffset + maxOffset);
            offset = new(0f, (maxOffset - minOffset) * 0.5f);
        }
        Vector2[] railVisualizationPoints = GeometryUtils.CenteredRectangle(railSize.x + railPadding, railSize.y + railPadding);
        railVisualization.OverridePolygonPoints(railVisualizationPoints);
        railVisualization.gameObject.transform.position = position + offset;
        railVisualization.rbInput.localLinkPosOtherRB = (position - borderBoundaryCenter) + offset;
        railVisualization.rbInput.includeInSimulation = doRenderRail;

        // Other properties
        borderCollider.rbInput.mass = mass;
        sceneCollider.rbInput.mass = mass;
        if (colliderType == ColliderType.None)
        {
            Debug.LogWarning("Collider type set to 'None'. This is not allowed for mechanical switches. Defaulting to 'All'");
            colliderType = ColliderType.All;
        }
        sceneCollider.rbInput.colliderType = colliderType;
    }
}