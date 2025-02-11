using UnityEngine;
using System.Collections.Generic;
using Resources2;

[ExecuteInEditMode]
public class SplineCurveDrawer : EditorLifeCycle
{
    [Header("Curve Settings")]
    [SerializeField] private RectTransform[] handleRects;
    [SerializeField] private Vector2 curveOffset = Vector2.zero;
    [SerializeField] private int segmentsPerCurve = 10;
    [SerializeField] private Color editorCanvasCurveColor = Color.green;
    [SerializeField] private Color editorSimCurveColor = Color.blue;
    [SerializeField] private float boxBottom = 0f;

    [SerializeField, HideInInspector] private Vector3[] lastHandlePositions;

    // Private references
    private Main main;

    private void OnDrawGizmos()
    {
        if (handleRects == null || handleRects.Length < 2) return;

        DrawBoxSplineInEditor(editorCanvasCurveColor, false); // Canvas space
        DrawBoxSplineInEditor(editorSimCurveColor, true); // Sim space
    }

    public override void OnEditorUpdate()
    {
        if (!Application.isPlaying)
        {
            if (handleRects != null && handleRects.Length > 0)
            {
                lastHandlePositions = new Vector3[handleRects.Length];
                for (int i = 0; i < handleRects.Length; i++)
                {
                    if (handleRects[i] != null)
                        lastHandlePositions[i] = handleRects[i].position;
                }
            }
        }
    }

    private void DrawBoxSplineInEditor(Color color, bool simSpace_canvasSpace)
    {
        Gizmos.color = color;
        
        Vector3[] splinePoints = CreateBoxSplinePoints(simSpace_canvasSpace, true);
        if (splinePoints.Length < 2) return;
        
        for (int i = 0; i < splinePoints.Length - 1; i++)
        {
            Gizmos.DrawLine(splinePoints[i], splinePoints[i + 1]);
        }
    }
 
    public Vector3[] CreateSplinePoints(bool simSpace_canvasSpace)
    {
        List<Vector3> points = new List<Vector3>();

        Vector3[] positions = new Vector3[handleRects.Length];
        for (int i = 0; i < handleRects.Length; i++)
        {
            if (Application.isPlaying && lastHandlePositions != null && lastHandlePositions.Length == handleRects.Length)
                positions[i] = lastHandlePositions[i];
            else if (handleRects[i] != null)
                positions[i] = handleRects[i].position;
        }
        
        for (int i = 0; i < positions.Length - 1; i++)
        {
            Vector3 p0 = positions[Mathf.Max(i - 1, 0)];
            Vector3 p1 = positions[i];
            Vector3 p2 = positions[i + 1];
            Vector3 p3 = positions[Mathf.Min(i + 2, positions.Length - 1)];
            
            if (points.Count == 0)
            {
                points.Add(TransformPoint(p1, simSpace_canvasSpace));
            }
            
            for (int j = 1; j <= segmentsPerCurve; j++)
            {
                float t = j / (float)segmentsPerCurve;
                Vector3 point = Func.CatmullRom(p0, p1, p2, p3, t);
                points.Add(TransformPoint(point, simSpace_canvasSpace));
            }
        }
        
        return points.ToArray();
    }

    public Vector3[] CreateBoxSplinePoints(bool simSpace_canvasSpace, bool closeLoop)
    {
        Vector3[] topCurve = CreateSplinePoints(simSpace_canvasSpace);
        if (topCurve.Length < 2)
            return topCurve;
        
        List<Vector3> points = new List<Vector3>();
        points.AddRange(topCurve);
        float bottomY = GetBoxBottom(simSpace_canvasSpace);
        Vector3 lastTop = topCurve[^1];
        points.Add(new Vector3(lastTop.x, bottomY, lastTop.z));
        Vector3 firstTop = topCurve[0];
        points.Add(new Vector3(firstTop.x, bottomY, firstTop.z));
        if (closeLoop) points.Add(firstTop);
        
        return points.ToArray();
    }

    private float GetBoxBottom(bool simSpace_canvasSpace)
    {
        if (simSpace_canvasSpace)
            return boxBottom;
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        Vector2 res = Func.Int2ToVector2(main.Resolution);
        Vector2 boundary = Func.Int2ToVector2(main.BoundaryDims);
        return boxBottom / boundary.y * res.y;
    }

    private Vector3 TransformPoint(Vector3 point, bool simSpace_canvasSpace)
    {
        point += (Vector3)curveOffset;
        if (simSpace_canvasSpace) CanvasSpaceToSimSpace(ref point);

        return point;
    }
 
    private void CanvasSpaceToSimSpace(ref Vector3 coords)
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        coords = (Vector2)coords / Func.Int2ToVector2(main.Resolution) * Func.Int2ToVector2(main.BoundaryDims);
    }
}
