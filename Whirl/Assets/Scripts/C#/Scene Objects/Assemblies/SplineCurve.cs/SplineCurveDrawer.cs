using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using Resources2;

[ExecuteAlways]
public class SplineCurveDrawer : MonoBehaviour
{
    [Header("Curve Settings")]
    [SerializeField] private RectTransform[] handleRects;
    [SerializeField] private Vector2 curveOffset = Vector2.zero;
    [SerializeField] private int segmentsPerCurve = 10;
    [SerializeField] private Color editorCanvasCurveColor = Color.green;
    [SerializeField] private Color editorSimCurveColor = Color.blue;
    [SerializeField] private float boxBottom = 0f;
    [SerializeField] private float boxLeft = 0f;
    [SerializeField] private float boxRight = 0f;

    [Header("Unity Event")]
    public UnityEvent onValueChangeDone;

    [Header("References")]
    [SerializeField] private DataStorage dataStorage;
    [SerializeField] private DataStorage dataStorageB;

    [SerializeField, HideInInspector] private Vector3[] lastHandlePositions;
    [SerializeField] private bool disableChildrenOnStart = false;

    // Private references
    private Main main;

    // Other
    private bool storedDataInitialized = false;
    private bool isMonitoringValueChange = false;
    private float[] previousSliderValues;

    private void Start()
    {
        if (disableChildrenOnStart && Application.isPlaying)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                transform.GetChild(i).gameObject.SetActive(false);
            }
        }
    }

    private void CheckStoredData()
    {
        if (Application.isPlaying && !storedDataInitialized)
        {
            float[] storedSliderValues = dataStorage.GetValue<float[]>();
            if (storedSliderValues != null && handleRects != null && storedSliderValues.Length == handleRects.Length)
            {
                for (int i = 0; i < handleRects.Length; i++)
                {
                    if (handleRects[i] != null && !disableChildrenOnStart)
                    {
                        Slider slider = handleRects[i].GetComponentInParent<Slider>();
                        if (slider != null)
                        {
                            slider.value = storedSliderValues[i];
                        }
                    }
                }
            }
            Vector3[] storedPositions = dataStorageB.GetValue<Vector3[]>();
            if (storedPositions != null && handleRects != null && storedPositions.Length == handleRects.Length)
            {
                lastHandlePositions = storedPositions;
            }
            storedDataInitialized = true;
        }
    }

    private void OnDrawGizmos()
    {
        if (handleRects == null || handleRects.Length < 2) return;

        DrawBoxSplineInEditor(editorCanvasCurveColor, false); // Canvas space
        DrawBoxSplineInEditor(editorSimCurveColor, true); // Sim space
    }

    public void Update()
    {
        if (handleRects != null && handleRects.Length > 0)
        {
            float[] sliderValues = new float[handleRects.Length];
            for (int i = 0; i < handleRects.Length; i++)
            {
                if (handleRects[i] != null && !disableChildrenOnStart)
                {
                    Slider slider = handleRects[i].GetComponentInParent<Slider>();
                    if (slider != null)
                    {
                        sliderValues[i] = slider.value;
                    }
                }
            }
            
            if (previousSliderValues == null || previousSliderValues.Length != sliderValues.Length)
            {
                previousSliderValues = sliderValues;
            }
            else
            {
                bool valueChanged = false;
                for (int i = 0; i < sliderValues.Length; i++)
                {
                    if (sliderValues[i] != previousSliderValues[i])
                    {
                        valueChanged = true;
                        break;
                    }
                }
                if (valueChanged && !isMonitoringValueChange)
                {
                    StartMonitoringValueChange();
                }
                previousSliderValues = sliderValues;
            }
            
            if(dataStorage != null)
                dataStorage.SetValue(sliderValues);

            lastHandlePositions = new Vector3[handleRects.Length];
            for (int i = 0; i < handleRects.Length; i++)
            {
                if (handleRects[i] != null)
                    lastHandlePositions[i] = handleRects[i].position;
            }
            if(dataStorageB != null)
                dataStorageB.SetValue(lastHandlePositions);
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
        CheckStoredData();

        List<Vector3> points = new();

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
        CheckStoredData();

        Vector3[] topCurve = CreateSplinePoints(simSpace_canvasSpace);
        if (topCurve.Length < 2)
            return topCurve;
        
        List<Vector3> points = new();
        
        // Add the top curve points
        points.AddRange(topCurve);

        // Compute extrusion values using shared logic
        float extrudedBottomY = GetExtrusionValue(boxBottom, simSpace_canvasSpace, false);
        float leftOffset = GetExtrusionValue(boxLeft, simSpace_canvasSpace, true);
        float rightOffset = GetExtrusionValue(boxRight, simSpace_canvasSpace, true);

        Vector3 firstTop = topCurve[0];
        Vector3 lastTop = topCurve[topCurve.Length - 1];

        // Create extruded vertices for straight, perpendicular side edges
        Vector3 leftTop = new Vector3(firstTop.x + leftOffset, firstTop.y, firstTop.z);
        Vector3 rightTop = new Vector3(lastTop.x + rightOffset, lastTop.y, lastTop.z);
        Vector3 leftBottom = new Vector3(firstTop.x + leftOffset, extrudedBottomY, firstTop.z);
        Vector3 rightBottom = new Vector3(lastTop.x + rightOffset, extrudedBottomY, lastTop.z);

        // Construct the polygon:
        // top curve -> right edge (top then bottom) -> bottom edge -> left edge (bottom then top) -> close
        points.Add(rightTop);
        points.Add(rightBottom);
        points.Add(leftBottom);
        points.Add(leftTop);
        if (closeLoop) points.Add(firstTop);
        
        return points.ToArray();
    }

    private float GetExtrusionValue(float value, bool simSpace, bool isHorizontal)
    {
        if (simSpace)
            return value;
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        Vector2 res = Func.Int2ToVector2(main.Resolution);
        Vector2 boundary = Func.Int2ToVector2(main.BoundaryDims);
        return isHorizontal ? value / boundary.x * res.x : value / boundary.y * res.y;
    }

    private float GetBoxBottom(bool simSpace_canvasSpace)
    {
        return GetExtrusionValue(boxBottom, simSpace_canvasSpace, false);
    }

    private float GetBoxLeft(bool simSpace_canvasSpace)
    {
        return GetExtrusionValue(boxLeft, simSpace_canvasSpace, true);
    }

    private float GetBoxRight(bool simSpace_canvasSpace)
    {
        return GetExtrusionValue(boxRight, simSpace_canvasSpace, true);
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
    
    public void StartMonitoringValueChange()
    {
        if (!isMonitoringValueChange)
            StartCoroutine(WaitForMouseRelease());
    }
    
    private IEnumerator WaitForMouseRelease()
    {
        isMonitoringValueChange = true;
        while (Input.GetMouseButton(0))
        {
            yield return null;
        }
        onValueChangeDone.Invoke();
        isMonitoringValueChange = false;
    }
}