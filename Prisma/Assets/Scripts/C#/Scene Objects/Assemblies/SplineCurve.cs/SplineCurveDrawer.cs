using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;
using Resources2;

[ExecuteAlways]
public class SplineCurveDrawer : MonoBehaviour
{
    [Header("Handle References")]
    [SerializeField] private RectTransform[] handleRects;

    [Header("Editor Render")]
    [SerializeField] private bool doRenderInEditor = true;
    [SerializeField] private Color editorCanvasPointColor = Color.green;
    [SerializeField] private Color editorSimPointColor = Color.blue;
    [SerializeField] private Color editorCanvasCurveColor = Color.green;
    [SerializeField] private Color editorSimCurveColor = Color.blue;

    [Header("Positioning")]
    [SerializeField] private Vector2 curveOffset = Vector2.zero;
    [SerializeField] private float boxBottom = 0f;
    [SerializeField] private float boxLeft = 0f;
    [SerializeField] private float boxRight = 0f;
    
    [Header("Resolution & Pruning")]
    [SerializeField] private bool splineOnly = false;
    [SerializeField] private int segmentsPerCurve = 10;
    [SerializeField] private float angleThreshold = 2f;
    [SerializeField] private float dstThreshold = 0f;

    [Header("Unity Event")]
    public UnityEvent onValueChangeDone;

    [Header("References")]
    [SerializeField] private DataStorage dataStorage;
    [SerializeField] private DataStorage dataStorageB;

    [SerializeField, HideInInspector] private Vector3[] lastHandlePositions;
    [SerializeField] private bool disableChildrenOnStart = false;

    // Private references
    private Main main;
    private CanvasScaler canvas;

    // Other
    private bool storedDataInitialized = false;
    private bool isMonitoringValueChange = false;
    private float[] previousSliderValues;

    private void OnEnable() => CheckStoredData();

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
            float[] storedSliderValues = dataStorage?.GetValue<float[]>();
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
            Vector3[] storedPositions = dataStorageB?.GetValue<Vector3[]>();
            if (storedPositions != null && handleRects != null && storedPositions.Length == handleRects.Length)
            {
                lastHandlePositions = storedPositions;
            }
            storedDataInitialized = true;
        }
    }

    private void OnDrawGizmos()
    {
        if (!doRenderInEditor || handleRects == null || handleRects.Length < 2) return;

        DrawBoxSplineInEditor(editorCanvasPointColor, editorCanvasCurveColor, false); // Canvas space
        DrawBoxSplineInEditor(editorSimPointColor, editorSimCurveColor,true); // Sim space
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
            
            if(dataStorage != null) dataStorage.SetValue(sliderValues);

            lastHandlePositions = new Vector3[handleRects.Length];
            for (int i = 0; i < handleRects.Length; i++)
            {
                if (handleRects[i] != null)
                {
                    Vector3 simPos = handleRects[i].position;
                    CanvasSpaceToSimSpace(ref simPos);
                    lastHandlePositions[i] = simPos;
                }
            }
            if(dataStorageB != null) dataStorageB.SetValue(lastHandlePositions);
        }
    }

    private void DrawBoxSplineInEditor(Color pointColor, Color curveColor, bool simSpace_canvasSpace)
    {
        Vector3[] splinePoints = CreateBoxSplinePoints(simSpace_canvasSpace, true);
        if (splinePoints.Length < 2) return;
        
        for (int i = 0; i < splinePoints.Length - 1; i++)
        {
            Gizmos.color = pointColor;
            Gizmos.DrawSphere(splinePoints[i], simSpace_canvasSpace ? 2 : 8);
            Gizmos.color = curveColor;
            Gizmos.DrawLine(splinePoints[i], splinePoints[i + 1]);
        }
    }

    public Vector3[] CreateSplinePoints(bool simSpace_canvasSpace)
    {
        CheckStoredData();

        List<Vector3> points = new();

        Vector3[] positions = new Vector3[handleRects.Length];
        bool usingStored = Application.isPlaying && simSpace_canvasSpace && lastHandlePositions != null && lastHandlePositions.Length == handleRects.Length;
        for (int i = 0; i < handleRects.Length; i++)
        {
            if (usingStored)
                positions[i] = lastHandlePositions[i];
            else if (handleRects[i] != null)
                positions[i] = handleRects[i].position;
        }
        
        int numPositions = positions.Length;

        if (splineOnly) // Non-box spline: treat as a closed loop
        {
            for (int i = 0; i < numPositions; i++)
            {
                Vector3 p1 = positions[i];
                Vector3 p2 = positions[(i + 1) % numPositions];
                Vector3 p0 = (i == 0) ? positions[numPositions - 1] : positions[i - 1];
                Vector3 p3 = positions[(i + 2) % numPositions];

                if (points.Count == 0)
                {
                    points.Add(p1);
                }
                
                for (int j = 1; j <= segmentsPerCurve; j++)
                {
                    float t = j / (float)segmentsPerCurve;
                    Vector3 point = Func.CatmullRom(p0, p1, p2, p3, t);
                    points.Add(point);
                }
            }
        }
        else // Box spline: use an open curve that does not loop through the start and end
        {
            for (int i = 0; i < numPositions - 1; i++)
            {
                Vector3 p1 = positions[i];
                Vector3 p2 = positions[i + 1];
                Vector3 p0 = (i == 0) ? p1 * 2f - p2 : positions[i - 1];
                Vector3 p3 = (i == numPositions - 2) ? p2 * 2f - p1 : positions[i + 2];
                
                if (points.Count == 0)
                {
                    points.Add(p1);
                }
                
                for (int j = 1; j <= segmentsPerCurve; j++)
                {
                    float t = j / (float)segmentsPerCurve;
                    Vector3 point = Func.CatmullRom(p0, p1, p2, p3, t);
                    points.Add(point);
                }
            }
        }

        // Prune points
        int lastCount = -1;
        while (lastCount != points.Count)
        {
            lastCount = points.Count;
            PrunePoints(ref points);
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (usingStored)
                points[i] += (Vector3)curveOffset;
            else
                points[i] = TransformPoint(points[i], simSpace_canvasSpace);
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
        
        points.AddRange(topCurve);

        boxLeft = boxLeft == 0f ? -0.01f : boxLeft;
        boxRight = boxRight == 0f ? 0.01f : boxRight;

        if (!splineOnly)
        {
            float extrudedBottomY = GetExtrusionValue(boxBottom, simSpace_canvasSpace, false);
            float leftOffset = GetExtrusionValue(boxLeft, simSpace_canvasSpace, true);
            float rightOffset = GetExtrusionValue(boxRight, simSpace_canvasSpace, true);

            Vector3 firstTop = topCurve[0];
            Vector3 lastTop = topCurve[topCurve.Length - 1];

            Vector3 leftTop = new(firstTop.x + leftOffset, firstTop.y, firstTop.z);
            Vector3 rightTop = new(lastTop.x + rightOffset, lastTop.y, lastTop.z);
            Vector3 leftBottom = new(firstTop.x + leftOffset, extrudedBottomY, firstTop.z);
            Vector3 rightBottom = new(lastTop.x + rightOffset, extrudedBottomY, lastTop.z);
            
            points.Add(rightTop);
            points.Add(rightBottom);
            points.Add(leftBottom);
            points.Add(leftTop);
            if (closeLoop) points.Add(firstTop);
        }
        else if (closeLoop) points.Add(points[0]);
        
        return points.ToArray();
    }

    private float GetExtrusionValue(float value, bool simSpace, bool isHorizontal)
    {
        if (simSpace) return value;
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        if (canvas == null) canvas = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<CanvasScaler>();
        Vector2 boundary = Func.Int2ToVector2(main.BoundaryDims);
        return isHorizontal ? value / boundary.x * Screen.width : value / boundary.y * Screen.height;
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
        if (canvas == null) canvas = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<CanvasScaler>();
        coords = (Vector2)coords / new Vector2(Screen.width, Screen.height) * Func.Int2ToVector2(main.BoundaryDims);
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

    private void PrunePoints(ref List<Vector3> input)
    {
        if (input.Count < 3) return;
        List<Vector3> output = new()
        {
            input[0]
        };
        float lastAngle = 0;
        for (int i = 1; i < input.Count - 1; i++)
        {
            Vector3 prev = input[i];
            Vector3 next = input[i + 1];
            float newAngle = Func.AngleFromDir(prev - next);
            float deltaAngle = Func.AbsDeltaAngle(newAngle, lastAngle);
            lastAngle = newAngle;
            if (deltaAngle >= angleThreshold && (prev - next).magnitude > dstThreshold)
            {
                if ((output[^1] - input[i]).sqrMagnitude > 0.01) output.Add(input[i]);
            }
            else
            {
                if (++i < input.Count - 1) output.Add(input[++i]);
            }
        }
        if ((output[^1] - input[^1]).sqrMagnitude > 0.01) output.Add(input[^1]);
        input = output;
    }
}
