using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class EditorManager : Editor
{
    private const float sceneObjectLineThickness = 0.5f;
    private const float sceneObjectSensorLineThickness = 0.5f;
    private const float springsceneObjectLineThickness = 2.0f;
    private const float springAmplitude = 7.0f;
    private const int numSpringPoints = 15;
    private const float springForceFactor = 1 / 50000.0f;
    private const float hoveredPointLineLength = 1000f;
    private const float hoveredPointAxisLineThickness = 0.4f;

    private static readonly Color SpringBaseColor = Color.green;
    private static readonly Color SpringStressedColor = Color.red;
    private static readonly Color OrangeColor = new(1.0f, 0.15f, 0.0f);
    private static readonly Color HoveredPointDarkModeAxisColor = new(0.9f, 0.9f, 0.9f);

    void OnEnable() => EditorApplication.update += OnEditorUpdate;
    void OnDisable() => EditorApplication.update -= OnEditorUpdate;
    void OnEditorUpdate() {}

#region Draw Rigid Bodies
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawRigidBodyObjects(SceneRigidBody rigidBody, GizmoType gizmoType)
    {
        if (Application.isPlaying || rigidBody == null) return;

        rigidBody.SetPolygonData();

        // Apply parent offset
        Transform transform = rigidBody.transform;
        Vector2 parentOffset = transform.position - transform.localPosition;

        // Draw the filled polygon for each path
        if (rigidBody.doDrawBody)
        {
            foreach (List<Vector2> path in rigidBody.MeshPointsPerPath)
            {
                DrawFilledPolygon(path, rigidBody.BodyColor);
            }
        }

        // Draw the wireframe for each path
        foreach (List<Vector2> path in rigidBody.MeshPointsPerPath)
        {
            DrawMeshWireframe(path.ToArray(), rigidBody.LineColor, sceneObjectLineThickness);
        }

        // Check constraints
        bool isSpringConstraint = rigidBody.rbInput.constraintType == ConstraintType.Spring && rigidBody.rbInput.linkedRigidBody != null;
        bool isLinearMotor = rigidBody.rbInput.constraintType == ConstraintType.LinearMotor;
        bool isRigidConstraint = rigidBody.rbInput.constraintType == ConstraintType.Rigid && rigidBody.rbInput.linkedRigidBody != null;

        // Draw spring
        if (isSpringConstraint)
        {
            (Vector2 startPoint, Vector2 endPoint) = GetSpringEndPoints(rigidBody);

            // Determine color by how “stretched” the spring is
            Color lerpColor;
            bool autoSpringRestLength = rigidBody.rbInput.autoSpringRestLength;
            if (autoSpringRestLength)
            {
                lerpColor = SpringBaseColor;
                rigidBody.approximatedSpringLength = "Automatic Spring Length Active";
                rigidBody.approximatedSpringForce = "Automatic Spring Length Active";
            }
            else
            {
                float approxLength = Vector2.Distance(startPoint, endPoint);
                float approxForce = rigidBody.rbInput.springStiffness
                                    * Mathf.Abs(rigidBody.rbInput.springRestLength - approxLength);

                lerpColor = Color.Lerp(SpringBaseColor, SpringStressedColor,
                                       approxForce * springForceFactor);

                rigidBody.approximatedSpringLength = approxLength.ToString() + " sim l.e";
                rigidBody.approximatedSpringForce = approxForce.ToString() + " sim k.e";
            }

            // Draw the spring
            DrawZigZagSpring(startPoint, endPoint,
                             lerpColor, springsceneObjectLineThickness,
                             springAmplitude, numSpringPoints);

            DrawDot(startPoint, 2.5f, Color.red);
            DrawDot(endPoint, 2.5f, Color.red);
        }

        // Draw linear motor path
        else if (isLinearMotor)
        {
            Vector2 startPoint = rigidBody.rbInput.startPos + parentOffset;
            Vector2 endPoint   = rigidBody.rbInput.endPos   + parentOffset;

            // Draw dashed line
            DrawDashedLine(OrangeColor, endPoint, startPoint,
                           10, 3, rigidBody.editorLineAnimationSpeed);

            // Draw dots
            DrawDot(startPoint, 4.5f, new Color(1.0f, 1.0f, 0.0f));
            DrawDot(startPoint, 3.5f, new Color(1.0f, 0.05f, 0.0f));
            DrawDot(endPoint,   4.5f, new Color(1.0f, 1.0f, 0.0f));
            DrawDot(endPoint,   3.5f, new Color(1.0f, 0.05f, 0.0f));

            rigidBody.approximatedSpringLength = "No Active Spring Link";
            rigidBody.approximatedSpringForce  = "No Active Spring Link";
        }
        
        // Draw rigid link
        else if (isRigidConstraint)
        {
            Vector2 thisRBCentroid = rigidBody.cachedCentroid;
            Vector2 linkPos        = rigidBody.rbInput.linkedRigidBody.cachedCentroid
                                     + rigidBody.rbInput.localLinkPosOtherRB;
            Vector2 otherRBCentroid = rigidBody.rbInput.linkedRigidBody.cachedCentroid;

            // Draw dashed lines
            DrawDashedLine(OrangeColor, linkPos, thisRBCentroid, 10, 3,
                           rigidBody.editorLineAnimationSpeed);
            DrawDashedLine(OrangeColor, linkPos, otherRBCentroid, 10, 3,
                           rigidBody.editorLineAnimationSpeed);

            // Draw dots
            DrawDot(thisRBCentroid, 2.5f, Color.red);
            DrawDot(otherRBCentroid, 2.5f, Color.red);

            DrawDot(linkPos, 4.5f, new Color(1.0f, 1.0f, 0.0f));
            DrawDot(linkPos, 3.5f, new Color(1.0f, 0.05f, 0.0f));
        }
        else
        {
            rigidBody.approximatedSpringLength = "No Active Spring Link";
            rigidBody.approximatedSpringForce  = "No Active Spring Link";
        }

        DrawLinesFromHoveredPoint(rigidBody);
    }
#endregion

#region Draw Fluids
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidObjects(SceneFluid fluid, GizmoType gizmoType)
    {
        if (fluid == null || Application.isPlaying) return;

        fluid.SetPolygonData();

        if (fluid.editorRenderMethod == EditorRenderMethod.Particles)
        {
            fluid.Points = fluid.GeneratePoints(false, -1).ToArray();
            int iterationCount = 0;

            Gizmos.color = fluid.BodyColor;
            foreach (Vector2 point in fluid.Points)
            {
                if (iterationCount++ > fluid.MaxGizmosIterations) return;
                Gizmos.DrawSphere(point, fluid.editorPointRadius);
            }
        }
        else if (fluid.editorRenderMethod == EditorRenderMethod.Triangulation)
        {
            // Triangulate each path individually
            foreach (List<Vector2> path in fluid.MeshPointsPerPath)
            {
                DrawFilledPolygon(path, fluid.BodyColor);
            }
        }

        // Draw edges for each path
        foreach (Edge edge in fluid.Edges)
        {
            Vector3[] quadVertices = GetQuadVertices(edge.start, edge.end, sceneObjectLineThickness);
            Handles.DrawSolidRectangleWithOutline(quadVertices, fluid.LineColor, fluid.LineColor);
        }

        // Draw horizontal and vertical lines from hovered point
        DrawLinesFromHoveredPoint(fluid);
    }
#endregion

#region Draw Fluid Spawners
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidSpawnerObjects(FluidSpawner fluidSpawner, GizmoType gizmoType)
    {
        if (fluidSpawner == null || Application.isPlaying) return;

        fluidSpawner.SetPolygonData();

        // Draw body
        if (fluidSpawner.DoDrawBody)
        {
            foreach (List<Vector2> path in fluidSpawner.MeshPointsPerPath)
            {
                DrawFilledPolygon(path, fluidSpawner.BodyColor);
            }
        }

        // Draw wireframe
        foreach (List<Vector2> path in fluidSpawner.MeshPointsPerPath)
        {
            DrawMeshWireframe(path.ToArray(), fluidSpawner.LineColor, sceneObjectLineThickness);
        }
    }
#endregion

#region Draw Fluid Sensors
    [DrawGizmo(GizmoType.NotInSelectionHierarchy | GizmoType.Selected)]
    static void DrawFluidSensorObjects(FluidSensor fluidSensor, GizmoType gizmoType)
    {
        if (fluidSensor == null || Application.isPlaying) return;

        Vector2 min = fluidSensor.measurementZone.min;
        Vector2 max = min + new Vector2(fluidSensor.measurementZone.width, fluidSensor.measurementZone.height);

        if (min == max) return;

        Vector2 v1 = min;
        Vector2 v2 = new Vector2(min.x, max.y);
        Vector2 v3 = new Vector2(max.x, min.y);
        Vector2 v4 = max;

        Vector2[] quadVertices_Vector2 = { v1, v3, v4, v2 };
        Vector3[] quadVertices_Vector3 = { v1, v3, v4, v2 };

        // Wireframe
        DrawMeshWireframe(quadVertices_Vector2, fluidSensor.lineColor, sceneObjectSensorLineThickness);

        // Filled area
        Handles.color = fluidSensor.areaColor;
        Handles.DrawSolidRectangleWithOutline(quadVertices_Vector3, fluidSensor.areaColor, fluidSensor.areaColor);
    }
#endregion

#region Private class (Springs, Dots, Lines, Polygons)
    private static (Vector2 start, Vector2 end) GetSpringEndPoints(SceneRigidBody rigidBody)
    {
        SceneRigidBody otherRigidBody = rigidBody.rbInput.linkedRigidBody;

        if (rigidBody.cachedCentroid.x == float.PositiveInfinity) rigidBody.ComputeCentroid(rigidBody.defaultGridSpacing);
        if (otherRigidBody.cachedCentroid.x == float.PositiveInfinity) otherRigidBody.ComputeCentroid(otherRigidBody.defaultGridSpacing);

        Vector2 startPoint = rigidBody.cachedCentroid + rigidBody.rbInput.localLinkPosThisRB;
        Vector2 endPoint = otherRigidBody.cachedCentroid + rigidBody.rbInput.localLinkPosOtherRB;
        return (startPoint, endPoint);
    }

    private static void DrawZigZagSpring(
        Vector2 startPoint, Vector2 endPoint,
        Color color, float lineThickness,
        float amplitude, int pointCount)
    {
        Vector2 direction = (endPoint - startPoint).normalized;
        Vector2 perpendicular = new(-direction.y, direction.x);

        Vector2 lastPoint = startPoint;
        Handles.color = color;

        for (int i = 1; i < pointCount; i++)
        {
            float t = (float)i / (pointCount - 1);
            Vector2 pointOnLine = Vector2.Lerp(startPoint, endPoint, t);

            float offsetMultiplier = (i % 2 == 0) ? -1.0f : 1.0f;
            if (i == pointCount - 1) offsetMultiplier = 0.0f;

            Vector2 offsetVector = perpendicular * amplitude * offsetMultiplier;
            Vector2 currentPoint = pointOnLine + offsetVector;

            Vector3[] quadVertices = GetQuadVertices(lastPoint, currentPoint, lineThickness);
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);

            lastPoint = currentPoint;
        }
    }

    private static void DrawDot(Vector2 position, float size, Color color)
    {
        if (position == Vector2.positiveInfinity) return;
        Gizmos.color = color;
        Gizmos.DrawSphere(position, size);
    }

    private static void DrawDashedLine(
        Color lineColor, Vector3 from, Vector3 to,
        float dashLength, float lineThickness,
        float animationSpeed,
        bool drawArrowHead = false, float arrowHeadSize = 5.0f)
    {
        Vector3 direction = (to - from).normalized;
        float distance = Vector3.Distance(from, to);
        int dashCount = Mathf.CeilToInt(distance / dashLength);

        float offset = ((Time.realtimeSinceStartup * animationSpeed) % (dashLength * 2))
                       / dashLength * dashLength;

        Handles.color = lineColor;
        for (int i = 0; i < dashCount; i++)
        {
            float startOffset = i * dashLength * 2 - offset;
            float endOffset   = startOffset + dashLength;

            if (startOffset >= distance) break;
            if (endOffset <= 0) continue;

            startOffset = Mathf.Max(0, startOffset);
            endOffset   = Mathf.Min(distance, endOffset);

            Vector3 start = from + direction * startOffset;
            Vector3 end   = from + direction * endOffset;
            DrawThickLine(start, end, lineThickness);
        }

        if (drawArrowHead)
        {
            Vector3 arrowBase = to - direction * arrowHeadSize;
            Vector3 left  = Quaternion.AngleAxis(135, Vector3.forward)
                            * direction * arrowHeadSize * 0.5f;
            Vector3 right = Quaternion.AngleAxis(-135, Vector3.forward)
                            * direction * arrowHeadSize * 0.5f;
            Vector3[] triangle = { to, arrowBase + left, arrowBase + right };
            Handles.DrawAAConvexPolygon(triangle);
        }
    }

    private static void DrawThickLine(Vector3 start, Vector3 end, float thickness)
    {
        Vector3[] quad = GetQuadVertices(start, end, thickness);
        Handles.DrawAAConvexPolygon(quad);
    }

    private static Vector3[] GetQuadVertices(Vector2 start, Vector2 end, float thickness)
    {
        Vector2 direction = (end - start).normalized;
        Vector2 perpendicular = new Vector2(-direction.y, direction.x) * (thickness * 0.5f);

        Vector3[] quadVertices = new Vector3[4];
        quadVertices[0] = start + perpendicular;
        quadVertices[1] = start - perpendicular;
        quadVertices[2] = end - perpendicular;
        quadVertices[3] = end + perpendicular;
        return quadVertices;
    }

    private static void DrawMeshWireframe(Vector2[] meshVertices, Color color, float lineThickness)
    {
        int vertexCount = meshVertices.Length;
        Handles.color = color;

        for (int i = 0; i < vertexCount; i++)
        {
            Vector2 start = meshVertices[i];
            Vector2 end   = meshVertices[(i + 1) % vertexCount];

            Vector3[] quadVertices = GetQuadVertices(start, end, lineThickness);
            Handles.DrawSolidRectangleWithOutline(quadVertices, color, color);
        }
    }

    private static void DrawFilledPolygon(List<Vector2> meshPoints, Color fillColor)
    {
        if (meshPoints == null || meshPoints.Count < 3)
        {
            Debug.LogWarning("Cannot draw filled polygon with fewer than 3 points.");
            return;
        }

        Vector2[] polygonPoints = meshPoints.ToArray();
        Triangulator triangulator = new Triangulator(polygonPoints);
        int[] indices = triangulator.Triangulate();

        Vector3[] vertices = new Vector3[polygonPoints.Length];
        for (int i = 0; i < polygonPoints.Length; i++)
        {
            vertices[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);
        }

        Handles.color = fillColor;
        for (int i = 0; i < indices.Length; i += 3)
        {
            Vector3[] triangleVertices = new Vector3[3];
            triangleVertices[0] = vertices[indices[i + 0]];
            triangleVertices[1] = vertices[indices[i + 1]];
            triangleVertices[2] = vertices[indices[i + 2]];

            Handles.DrawAAConvexPolygon(triangleVertices);
        }
    }

    private static void DrawLinesFromHoveredPoint(Polygon polygonObj)
    {
        if (polygonObj == null || polygonObj.MeshPointsPerPath == null) return;

        Event currentEvent = Event.current;
        if (currentEvent == null) return;

        // Convert mouse position to world space
        Vector2 mousePosition = HandleUtility.GUIPointToWorldRay(currentEvent.mousePosition).origin;

        float closestDistance = float.MaxValue;
        Vector2 closestPoint = Vector2.positiveInfinity;

        // Go through all paths to find a path vertex near the mouse
        foreach (List<Vector2> path in polygonObj.MeshPointsPerPath)
        {
            foreach (Vector2 worldPt in path)
            {
                float dist = Vector2.Distance(mousePosition, worldPt);
                if (dist < 2f && dist < closestDistance)
                {
                    closestDistance = dist;
                    closestPoint = worldPt;
                }
            }
        }

        if (closestPoint == Vector2.positiveInfinity) return;

        ProgramLifeCycleManager lifeCycleManager = GameObject.FindGameObjectWithTag("LifeCycleManager")?.GetComponent<ProgramLifeCycleManager>();
        bool darkMode = lifeCycleManager != null && lifeCycleManager.darkMode;

        Handles.color = darkMode ? HoveredPointDarkModeAxisColor : Color.black;

        // Horizontal line
        Vector3 leftPoint  = new(-hoveredPointLineLength, closestPoint.y, 0);
        Vector3 rightPoint = new(hoveredPointLineLength,  closestPoint.y, 0);
        DrawThickLine(leftPoint, rightPoint, hoveredPointAxisLineThickness);

        // Vertical line
        Vector3 topPoint    = new(closestPoint.x,  hoveredPointLineLength, 0);
        Vector3 bottomPoint = new(closestPoint.x, -hoveredPointLineLength, 0);
        DrawThickLine(topPoint, bottomPoint, hoveredPointAxisLineThickness);
    }
#endregion
}