using UnityEngine;
using System.Collections;
using Resources2;

[ExecuteInEditMode]
public class WorkspacePanel : MonoBehaviour
{
    [Header("Panel Body")]
    [SerializeField] private float maskSize;
    [SerializeField] private float minPeekAmount;
    [SerializeField] private Vector2 bodySize;
    [SerializeField] private float lerpDuration = .35f;
    [SerializeField] private LerpCurve lerpCurve = LerpCurve.SmoothStep;
    [SerializeField] private ViewMode viewMode = ViewMode.Expanded;

    // NOTE: ExpDir4 now supports four directions: FromLeft, FromRight, FromTop, FromBottom.
    // Make sure your ExpDir4 enum is updated accordingly.
    [SerializeField] private ExpDir4 expandDir = ExpDir4.FromLeft;

    [Header("Handle")]
    public float handleBaseScale;
    public float hoverScaleMultiplier = 1.5f;
    public float hoverDuration = .12f;
    public LerpCurve hoverCurve = LerpCurve.SmoothStep;

    [Header("Persist")]
    [SerializeField] DataStorage dataStorage;

    [Header("References")]
    [SerializeField] private RectTransform maskTransform;
    [SerializeField] private RectTransform outerContainerTransform;
    [SerializeField] private Transform mainContainerTransform;
    [SerializeField] private Transform handleTransform;
    [SerializeField] private Transform handleIconTransform;

    private Coroutine _switchRoutine;
    private bool _isDragging;
    private float _minPos, _expPos; // along active axis only

    void Awake()
    {
        if (Application.isPlaying) LoadState();
    }

    private void Update()
    {
        if (!Application.isPlaying) InitDisplay();
        if (Application.isPlaying) SaveState();
    }

    public void InitDisplay()
    {
        if (outerContainerTransform != null && mainContainerTransform != null &&
            maskTransform != null && handleTransform != null)
        {
            // Size the body/mask to the container
            outerContainerTransform.sizeDelta = bodySize * mainContainerTransform.localScale;
            maskTransform.sizeDelta = outerContainerTransform.sizeDelta * mainContainerTransform.localScale;

            var dirSign = GetDirSign();
            var horizontal = IsHorizontal();

            // Offset the mask and main container so the content aligns with the mask opening
            if (horizontal)
            {
                maskTransform.localPosition = new Vector3(dirSign * maskSize, 0f, 0f);
                mainContainerTransform.localPosition = new Vector3(-dirSign * maskSize, 0f, 0f);

                // Handle sits at the exposed edge
                handleTransform.localPosition = new Vector3(dirSign * 0.5f * maskTransform.sizeDelta.x, 0f, 0f);
            }
            else
            {
                maskTransform.localPosition = new Vector3(0f, dirSign * maskSize, 0f);
                mainContainerTransform.localPosition = new Vector3(0f, -dirSign * maskSize, 0f);

                handleTransform.localPosition = new Vector3(0f, dirSign * 0.5f * maskTransform.sizeDelta.y, 0f);
            }

            handleTransform.localScale = new Vector3(handleBaseScale, handleBaseScale, 1f);
            handleTransform.rotation = Quaternion.Euler(0f, 0f, GetHandleBaseAngle());

            UpdatePositionInstant();
        }
        else
        {
            Debug.LogWarning("Missing references. Object: WorkspacePanel");
        }
    }

    public void SwitchViewMode()
    {
        viewMode = viewMode == ViewMode.Minimized ? ViewMode.Expanded : ViewMode.Minimized;

        if (!Application.isPlaying)
        {
            UpdatePositionInstant();
            return;
        }

        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = StartCoroutine(SwitchRoutine());
    }

    private float EvaluateCurve(float t)
    {
        return Lerp.Evaluate((Lerp.Curve)lerpCurve, t);
    }

    private IEnumerator SwitchRoutine()
    {
        (float expPos, float minPos) = GetMinExpView();
        float start = GetAxis(transform.localPosition);
        float target = viewMode == ViewMode.Expanded ? expPos : minPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / lerpDuration;
            float easedT = EvaluateCurve(Mathf.Clamp01(t));
            float v = Mathf.Lerp(start, target, easedT);

            transform.localPosition = SetAxis(transform.localPosition, v);

            UpdateHandleIcon(v, expPos, minPos);
            yield return null;
        }

        transform.localPosition = SetAxis(transform.localPosition, target);
        UpdateHandleIcon(target, expPos, minPos);
        _switchRoutine = null;
    }

    private void UpdatePositionInstant()
    {
        (float expView, float minView) = GetMinExpView();
        float v = viewMode == ViewMode.Expanded ? expView : minView;

        transform.localPosition = SetAxis(transform.localPosition, v);
        UpdateHandleIcon(v, expView, minView);
    }

    /// <summary>
    /// Computes expanded and minimized positions ALONG THE ACTIVE AXIS only, relative to the current placement.
    /// This removes the previous behavior of forcing the panel to a screen side.
    /// </summary>
    public (float expView, float minView) GetMinExpView()
    {
        // Slide distance the panel needs to move to fully hide except for the peek amount
        float scale = GetScaleAlongAxis();
        float size = GetMaskSizeAlongAxis();
        float slide = scale * size - maskSize * scale - minPeekAmount; // positive scalar distance

        int sign = GetDirSign();
        float current = GetAxis(transform.localPosition);

        // Determine the expanded position regardless of current mode
        float exp;
        if (viewMode == ViewMode.Expanded)
            exp = current;
        else
            exp = current - sign * slide; // reverse the minimize offset to get expanded

        float min = exp + sign * slide;
        return (exp, min);
    }

    private void UpdateHandleIcon(float current, float exp, float min)
    {
        if (handleIconTransform == null) return;

        float progress = Mathf.Approximately(min, exp) ? 0f : Mathf.Abs(current - exp) / Mathf.Abs(min - exp);
        float angle = Mathf.LerpAngle(0f, 180f, progress);

        // Rotate around a base angle so the chevron/arrow points correctly for each side
        handleIconTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void BeginDrag()
    {
        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        (_expPos, _minPos) = GetMinExpView();
        _isDragging = true;
    }

    /// <summary>
    /// Sets the dragged position along the ACTIVE AXIS. For horizontal panels pass X; for vertical panels pass Y.
    /// </summary>
    public void SetDraggedPosition(float newAxisPos)
    {
        if (!_isDragging) return;

        float clamped = Mathf.Clamp(newAxisPos, Mathf.Min(_minPos, _expPos), Mathf.Max(_minPos, _expPos));
        transform.localPosition = SetAxis(transform.localPosition, clamped);
        UpdateHandleIcon(clamped, _expPos, _minPos);
    }

    public void EndDragSnap()
    {
        if (!_isDragging) return;
        _isDragging = false;

        float cur = GetAxis(transform.localPosition);
        float distToExp = Mathf.Abs(cur - _expPos);
        float distToMin = Mathf.Abs(cur - _minPos);
        viewMode = distToExp < distToMin ? ViewMode.Expanded : ViewMode.Minimized;

        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = StartCoroutine(SwitchRoutine());
    }

    void SaveState()
    {
        if (!Application.isPlaying || dataStorage == null) return;
        // Store the axis position and the view mode. (For vertical panels, X stores the Y-axis value.)
        Vector2 payload = new Vector2(GetAxis(transform.localPosition), (int)viewMode);
        dataStorage.SetValue(payload);
    }

    void LoadState()
    {
        if (dataStorage == null || !DataStorage.hasValue) return;
        Vector2 payload = dataStorage.GetValue<Vector2>();
        if (payload != default)
        {
            viewMode = (ViewMode)(int)payload.y;

            // Restore along the active axis
            transform.localPosition = SetAxis(transform.localPosition, payload.x);

            (float exp, float min) = GetMinExpView();
            UpdateHandleIcon(payload.x, exp, min);

            if (_switchRoutine != null) StopCoroutine(_switchRoutine);
            _switchRoutine = StartCoroutine(SwitchRoutine());
        }
    }

    // =====================
    // Helpers
    // =====================

    private bool IsHorizontal()
    {
        return expandDir == ExpDir4.FromLeft || expandDir == ExpDir4.FromRight;
    }

    private int GetDirSign()
    {
        // Positive means minimize moves in +axis direction, negative means -axis
        switch (expandDir)
        {
            case ExpDir4.FromRight:
            case ExpDir4.FromTop:
                return +1;
            case ExpDir4.FromLeft:
            case ExpDir4.FromBottom:
            default:
                return -1;
        }
    }

    private float GetScaleAlongAxis()
    {
        return IsHorizontal() ? transform.localScale.x : transform.localScale.y;
    }

    private float GetMaskSizeAlongAxis()
    {
        return IsHorizontal() ? maskTransform.sizeDelta.x : maskTransform.sizeDelta.y;
    }

    private float GetAxis(Vector3 v)
    {
        return IsHorizontal() ? v.x : v.y;
    }

    private Vector3 SetAxis(Vector3 v, float value)
    {
        if (IsHorizontal())
            return new Vector3(value, v.y, v.z);
        else
            return new Vector3(v.x, value, v.z);
    }

    private float GetHandleBaseAngle()
    {
        switch (expandDir)
        {
            case ExpDir4.FromRight: return 0;
            case ExpDir4.FromTop: return 90f;
            case ExpDir4.FromBottom: return 270f;
            case ExpDir4.FromLeft:
            default: return 180;
        }
    }
}

public enum ExpDir4 { FromRight, FromTop, FromBottom, FromLeft }