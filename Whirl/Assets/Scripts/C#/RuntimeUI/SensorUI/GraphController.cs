using UnityEngine;
using ChartAndGraph;
using System;
using System.Collections.Generic;
using Resources2;
using PM = ProgramManager;

public class GraphController : MonoBehaviour
{
    // Serialized Fields
    [SerializeField] private bool overrideGraphDataCategory;
    [SerializeField] private bool isBezierCurve;
    [SerializeField, Range(0.0f, 1.0f)] private float bezierTension = 0.5f;
    [SerializeField, Range(1, 10)] private int HorizontalViewSize = 3;
    [SerializeField] private bool doUseAdaptiveViewY = true;
    [SerializeField, Range(0.0f, 0.2f)] private float adaptivePaddingYPercent = 0.1f;
    [SerializeField] private float fixedViewMinY = -10;
    [SerializeField] private float fixedViewMaxY = 10;
    [SerializeField] private Material lineMaterial;
    [SerializeField] private Material fillMaterial;
    [SerializeField] private Material pointMaterial;
    [SerializeField] private float lineThickness;
    [SerializeField] private float pointSize;

    // Other
    [NonSerialized] public bool isPointerHovering;

    // References
    [NonSerialized] private GraphChart graphChart;
    [NonSerialized] private ItemLabels itemLabels;
    [NonSerialized] private VerticalAxis verticalAxis;
    [NonSerialized] private HorizontalAxis horizontalAxis;

    // Private
    private List<Vector2> currentPoints;
    private List<Vector2> pendingPoints;
    private bool isFirstPointDrawn;
    private Timer pointSubmissionTimer;

    public void InitGraph(GraphChart graphChartInput, ItemLabels itemLabels, VerticalAxis verticalAxis, HorizontalAxis horizontalAxis, int numGraphDecimals, int numGraphTimeDecimals)
    {
        this.graphChart = graphChartInput;
        this.itemLabels = itemLabels;
        this.verticalAxis = verticalAxis;
        this.horizontalAxis = horizontalAxis;
        if (overrideGraphDataCategory && isBezierCurve)
        {
            Debug.LogWarning("The bezier curve setting cannot be combined with graph data category override. The graph data category will not be overridden");
            overrideGraphDataCategory = false;
        }
        if (overrideGraphDataCategory)
        {
            graphChart.DataSource.AddCategory("SensorDatas", lineMaterial, lineThickness, new MaterialTiling(), fillMaterial, false, pointMaterial, pointSize, false);
        }

        float pointSubmissionFrequency = Func.MsToSeconds(PM.Instance.sensorManager.msGraphPointSubmissionFrequency);
        pointSubmissionTimer = new Timer(pointSubmissionFrequency, TimeType.Scaled, false);
        
        horizontalAxis.MainDivisions.Total = HorizontalViewSize; // Each division is seperated by 1 second

        SetNumGraphDecimals(numGraphDecimals, numGraphTimeDecimals);

        ResetGraph();
    }

    public void ResetGraph()
    {
        // Clear the "SensorDatas" category if it exists
        if (graphChart.DataSource.HasCategory("SensorDatas"))
        {
            graphChart.DataSource.ClearCategory("SensorDatas");
        }

        // Reset data lists and flags
        currentPoints = new();
        pendingPoints = new();
        isFirstPointDrawn = false;
        pointSubmissionTimer.Reset();
    }

    public void SetSuffix(string suffix)
    {
        itemLabels.textFormat = new()
        {
            suffix = " " + suffix
        };
    }

    public void SetVerticalAxisSuffix(string suffix)
    {
        verticalAxis.MainDivisions.TextSuffix = suffix;
    }

    public void SetNumGraphDecimals(int numDecimals, int numGraphTimeDecimals)
    {
        itemLabels.FractionDigits = numDecimals;
        verticalAxis.MainDivisions.FractionDigits = numDecimals;
        horizontalAxis.MainDivisions.FractionDigits = numGraphTimeDecimals;

        verticalAxis.MainDivisions.TextSeperation = -40 - numDecimals * 10f;
    }

    public void SetXViewRange(float minX, float sizeX)
    {
        graphChart.ScrollableData.HorizontalViewOrigin = minX;
        graphChart.ScrollableData.HorizontalViewSize = sizeX;

        graphChart.ScrollableData.AutomaticHorizontalView = false;
    }

    public void SetYViewRange(float minY, float sizeY)
    {
        graphChart.ScrollableData.VerticalViewOrigin = minY;
        graphChart.ScrollableData.VerticalViewSize = sizeY;

        graphChart.ScrollableData.AutomaticVerticallView = false;
    }

    public void AddPointsToGraph(params Vector2[] points)
    {
        if (points == null || points.Length == 0) return;
        
        // Add points to pendingPoints if they are valid
        foreach (Vector2 point in points)
        {
            if (float.IsNaN(point.x) || float.IsNaN(point.y)) continue;

            if (!pointSubmissionTimer.Check()) break;

            pendingPoints.Add(point);
        }
    }

    public void UpdateGraph()
    {
        // Exit early if the program is paused, pointer is hovering, or there are no stored points
        if (PM.Instance.programPaused || isPointerHovering || pendingPoints.Count == 0) return;

        // Current X-axis view origin and size
        float currentMinX = Mathf.Max(PM.Instance.totalScaledTimeElapsed - HorizontalViewSize, 0f);
        float currentMaxX = currentMinX + HorizontalViewSize;

        // Add stored points to currentPoints
        foreach (Vector2 point in pendingPoints)
        {
            // Only add points within the current X-view range
            if (point.x >= currentMinX && point.x <= currentMaxX)
            {
                currentPoints.Add(currentPoints.Count == 0 ? new Vector2(point.x > 2.0f ? point.x : 0, point.y) : point);
            }

            if (isBezierCurve)
            {
                if (currentPoints.Count < 2) continue;

                int i = currentPoints.Count - 2;

                Vector2 p0 = i > 0 ? currentPoints[i - 1] : currentPoints[i];
                Vector2 p1 = currentPoints[i];
                Vector2 p2 = currentPoints[i + 1];
                Vector2 p3 = i + 2 < currentPoints.Count ? currentPoints[i + 2] : currentPoints[i + 1];

                if (!isFirstPointDrawn)
                {
                    isFirstPointDrawn = true;
                    graphChart.DataSource.SetCurveInitialPoint("SensorDatas", p1.x, p1.y);
                }

                // Calculate control points using a Catmull-Rom spline
                Vector2 controlPointA = p1 + (p2 - p0) * (bezierTension / 3f);
                Vector2 controlPointB = p2 - (p3 - p1) * (bezierTension / 3f);

                // Add the curve segment for the new point
                graphChart.DataSource.AddCurveToCategory(
                    "SensorDatas",
                    new DoubleVector2(controlPointA.x, controlPointA.y),
                    new DoubleVector2(controlPointB.x, controlPointB.y),
                    new DoubleVector2(p2.x, p2.y),
                    pointSize: -1f
                );
            }
            else
            {
                graphChart.DataSource.AddPointToCategory("SensorDatas", point.x, point.y);
            }
        }

        // Clear stored points after processing
        pendingPoints.Clear();

        // Remove points from currentPoints that are outside the current X-view range
        RemoveOldPoints(currentMinX);

        if (doUseAdaptiveViewY) CalculateAndSetDynamicYRange(currentMinX, currentMaxX);
        else SetYViewRange(fixedViewMinY, fixedViewMaxY - fixedViewMinY);

        SetXViewRange(currentMinX, HorizontalViewSize);
    }

    private void RemoveOldPoints(float currentMinX)
    {
        // Find the index where points start to be within the current X-view
        int firstValidIndex = 0;
        for (int i = 0; i < currentPoints.Count; i++)
        {
            if (currentPoints[i].x >= currentMinX)
            {
                firstValidIndex = i;
                break;
            }
        }

        // Remove all points before the firstValidIndex
        if (firstValidIndex > 0)
        {
            currentPoints.RemoveRange(0, firstValidIndex);
        }
    }

    private void CalculateAndSetDynamicYRange(float currentMinX, float currentMaxX)
    {
        // Collect points within the current X-view range
        List<Vector2> visiblePoints = new();

        foreach (Vector2 point in currentPoints)
        {
            if (point.x >= currentMinX && point.x <= currentMaxX)
            {
                visiblePoints.Add(point);
            }
        }

        if (visiblePoints.Count == 0)
        {
            // Default Y range if no points are visible
            SetYViewRange(-10f, 20f);
            return;
        }

        // Find minY and maxY from visible points
        float minY = float.MaxValue;
        float maxY = float.MinValue;
        foreach (Vector2 point in visiblePoints)
        {
            if (point.y < minY) minY = point.y;
            if (point.y > maxY) maxY = point.y;
        }

        float padding = (maxY - minY) * adaptivePaddingYPercent;
        minY -= padding;
        maxY += padding;
        float sizeY = maxY - minY;

        // Set the dynamic Y-axis range
        SetYViewRange(minY, sizeY);
    }
}
