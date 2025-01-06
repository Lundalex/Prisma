using UnityEngine;
using System.Collections.Generic;
using System;
using Resources2;

[ExecuteAlways]
public abstract class Polygon : EditorLifeCycle
{
    [Header("Editor Settings")]
    public bool snapPointToGrid = true;
    public float gridSpacing = 1.0f;
    public Color LineColor = Color.black;
    public Color BodyColor = Color.white;

    [NonSerialized] public List<Edge> Edges = new();
    [NonSerialized] public List<Vector2> MeshPoints = new(); // All points combined
    [NonSerialized] public List<List<Vector2>> MeshPointsPerPath = new(); // Multi-path drawing
    [NonSerialized] public PolygonCollider2D polygonCollider;

#if UNITY_EDITOR
    public override abstract void OnEditorUpdate();

    public virtual void SnapPointsToGrid()
    {
        Transform colliderTransform = polygonCollider.transform;
        int pathCount = polygonCollider.pathCount;
        for (int p = 0; p < pathCount; p++)
        {
            Vector2[] pathPoints = polygonCollider.GetPath(p);
            for (int i = 0; i < pathPoints.Length; i++)
            {
                Vector2 worldPoint = colliderTransform.TransformPoint(pathPoints[i]);
                worldPoint = new Vector2(
                    Mathf.Round(worldPoint.x / gridSpacing) * gridSpacing,
                    Mathf.Round(worldPoint.y / gridSpacing) * gridSpacing
                );
                pathPoints[i] = colliderTransform.InverseTransformPoint(worldPoint);
            }
            polygonCollider.SetPath(p, pathPoints);
        }
    }
#endif

    public void SetPolygonData(Vector2? offsetInput = null)
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();
        Vector2 offset = offsetInput ?? Vector2.zero;
    
        if (Application.isPlaying)
        {
            ValidatePolygonPointsOrderMultiPath();
        }

        Edges.Clear();
        MeshPoints.Clear();
        MeshPointsPerPath.Clear();

        // Get edges and meshPoints from all paths
        int pathCount = polygonCollider.pathCount;
        for (int p = 0; p < pathCount; p++)
        {
            Vector2[] localPoints = polygonCollider.GetPath(p);
            List<Vector2> currentPath = new();

            // Create points
            for (int i = 0; i < localPoints.Length; i++)
            {
                Vector2 worldPt = transform.TransformPoint(localPoints[i] + offset);
                MeshPoints.Add(worldPt);
                currentPath.Add(worldPt);
            }
            MeshPointsPerPath.Add(currentPath);

            // Create edges
            for (int i = 0; i < localPoints.Length; i++)
            {
                Vector2 startPoint = transform.TransformPoint(localPoints[i] + offset);
                Vector2 endPoint = transform.TransformPoint(localPoints[(i + 1) % localPoints.Length] + offset);
                Edges.Add(new Edge(startPoint, endPoint));
            }
        }
    }

    private void ValidatePolygonPointsOrderMultiPath()
    {
        // Ensure each path is counter clockwise by reversing if clockwise
        int pathCount = polygonCollider.pathCount;
        for (int i = 0; i < pathCount; i++)
        {
            // Get path points (local space)
            Vector2[] pathPoints = polygonCollider.GetPath(i);

            // Create a deep copy for world space transformation
            Vector2[] worldPathPoints = new Vector2[pathPoints.Length];
            for (int j = 0; j < pathPoints.Length; j++)
            {
                worldPathPoints[j] = transform.TransformPoint(pathPoints[j]);
            }

            // Ensure the point ordering is CCW
            if (GeometryUtils.IsClockwise(worldPathPoints))
            {
                Array.Reverse(pathPoints);
                polygonCollider.SetPath(i, pathPoints);
            }
        }
    }

    public void OverridePolygonPoints(Vector2[] points, int path = 0)
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();
        polygonCollider.SetPath(path, points);
    }

    public bool IsPointInsidePolygon(Vector2 point)
    {
        if (Edges.Count == 0) SetPolygonData();

        int intersectionCount = 0;
        foreach (Edge edge in Edges)
        {
            Vector2 p1 = edge.start;
            Vector2 p2 = edge.end;

            // Skip horizontal edges
            if (Mathf.Approximately(p1.y, p2.y)) continue;

            // Check ray intersection
            if ((point.y > Mathf.Min(p1.y, p2.y)) && (point.y <= Mathf.Max(p1.y, p2.y)))
            {
                float xIntersection = (p2.x - p1.x) * (point.y - p1.y) / (p2.y - p1.y) + p1.x;
                if (xIntersection > point.x) intersectionCount++;
            }
        }

        // Odd -> inside, Even -> outside
        return (intersectionCount % 2) == 1;
    }

    public void CenterPolygonPosition()
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();

        // Collect all points
        List<Vector2> allPoints = new();
        int pathCount = polygonCollider.pathCount;
        for (int p = 0; p < pathCount; p++)
        {
            Vector2[] pathPoints = polygonCollider.GetPath(p);
            allPoints.AddRange(pathPoints);
        }

        if (allPoints.Count == 0) return;

        // Compute local centroid
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 pt in allPoints) centroid += pt;
        centroid /= allPoints.Count;

        // Shift transform by that centroid in world space
        Vector3 worldCentroidOffset = transform.TransformVector(centroid);
        transform.position += worldCentroidOffset;

        // Then offset each path so that centroid is local(0,0)
        for (int p = 0; p < pathCount; p++)
        {
            Vector2[] pathPoints = polygonCollider.GetPath(p);
            for (int i = 0; i < pathPoints.Length; i++)
            {
                pathPoints[i] -= centroid;
            }
            polygonCollider.SetPath(p, pathPoints);
        }
    }
}