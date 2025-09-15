using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Resources2;

[ExecuteInEditMode]
public class MultiContainer : MonoBehaviour
{
    [Header("Panel Body")]
    [SerializeField] private float maskSize;
    [SerializeField] private float minPeekAmount;
    [SerializeField] private Vector2 bodySize;
    [SerializeField] private float lerpDuration = .35f;
    [SerializeField] private LerpCurve lerpCurve = LerpCurve.SmoothStep;
    [SerializeField] private ViewMode viewMode = ViewMode.Expanded;
    [SerializeField] private ExpDir expandDir = ExpDir.FromLeft;

    [Header("Handle")]
    public float handleBaseScale;
    public float hoverScaleMultiplier = 1.5f;
    public float hoverDuration = .12f;
    public LerpCurve hoverCurve = LerpCurve.SmoothStep;

    [Header("Persist")]
    [SerializeField] DataStorage dataStorage;

    [Header("References")]
    [ColorUsage(true, true)] public Color primaryColor;
    [SerializeField] private Image containerTrimImage;
    [SerializeField] private RectTransform maskTransform;
    [SerializeField] private RectTransform outerContainerTransform;
    [SerializeField] private RectTransform innerContainerTransform;
    [SerializeField] private RectTransform stretchContent;
    [SerializeField] private Transform mainContainerTransform;
    [SerializeField] private Transform handleTransform;
    [SerializeField] private Transform handleIconTransform;

    [Header("Stretch Content")]
    [SerializeField] private float stretchTop = 0f;
    [SerializeField] private float stretchBottom = 0f;

    private Main main;
    private Coroutine _switchRoutine;
    private bool _isDragging;
    private float _minX, _expX;

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
            outerContainerTransform.sizeDelta = bodySize * mainContainerTransform.localScale;
            maskTransform.sizeDelta = outerContainerTransform.sizeDelta * mainContainerTransform.localScale;

            float dir = expandDir == ExpDir.FromLeft ? 1f : -1f;

            maskTransform.localPosition = new Vector3(dir * maskSize, 0f, 0f);
            mainContainerTransform.localPosition = new Vector3(-dir * maskSize, 0f, 0f);

            handleTransform.localPosition = new Vector3(dir * 0.5f * maskTransform.sizeDelta.x, 0f, 0f);
            handleTransform.localScale = new Vector3(handleBaseScale, handleBaseScale, 1f);
            handleTransform.rotation = expandDir == ExpDir.FromRight ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.Euler(0f, 0f, 0f);

            ApplyStretchContent();
            UpdatePositionInstant();
        }
        else
        {
            Debug.LogWarning("Missing references. Object: MultiContainer");
        }
    }

    private void ApplyStretchContent()
    {
        // Requires stretchContent to be a child of innerContainerTransform.
        if (stretchContent == null || innerContainerTransform == null) return;
        if (stretchContent.parent != innerContainerTransform) return;

        Vector2 aMin = stretchContent.anchorMin;
        Vector2 aMax = stretchContent.anchorMax;
        aMin.y = 0f;
        aMax.y = 1f;
        stretchContent.anchorMin = aMin;
        stretchContent.anchorMax = aMax;

        Vector2 offMin = stretchContent.offsetMin;
        Vector2 offMax = stretchContent.offsetMax;
        offMin.y = stretchBottom;   // bottom padding
        offMax.y = -stretchTop;     // top padding (negative)
        stretchContent.offsetMin = offMin;
        stretchContent.offsetMax = offMax;
    }

    public void SwitchViewMode()
    {
        viewMode = viewMode == ViewMode.Minimized
                ? ViewMode.Expanded
                : ViewMode.Minimized;

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
        (float expX, float minX) = GetMinExpView();
        float startX  = transform.localPosition.x;
        float targetX = viewMode == ViewMode.Expanded ? expX : minX;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / lerpDuration;
            float easedT = EvaluateCurve(Mathf.Clamp01(t));
            float x = Mathf.Lerp(startX, targetX, easedT);

            transform.localPosition = new Vector3(x, transform.localPosition.y);

            UpdateHandleIcon(x, expX, minX);

            yield return null;
        }

        transform.localPosition = new Vector3(targetX, transform.localPosition.y, transform.localPosition.z);
        UpdateHandleIcon(targetX, expX, minX);

        _switchRoutine = null;
    }

    private void UpdatePositionInstant()
    {
        (float expView, float minView) = GetMinExpView();
        float x = viewMode == ViewMode.Expanded ? expView : minView;

        transform.localPosition = new Vector3(x, transform.localPosition.y);

        UpdateHandleIcon(x, expView, minView);
    }

    public (float expView, float minView) GetMinExpView()
    {
        if (main == null)
            main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        float dir = expandDir == ExpDir.FromLeft ? 1f : -1f;

        float maskedWidth = transform.localScale.x * (maskTransform.sizeDelta.x - 2f * maskSize);
        float expViewLeft = -0.5f * main.DefaultResolution.x + 0.5f * maskedWidth - 1f;

        float minViewLeft = expViewLeft
                        - transform.localScale.x * maskTransform.sizeDelta.x
                        + maskSize * transform.localScale.x
                        + minPeekAmount;

        return (dir * expViewLeft, dir * minViewLeft);
    }

    private void UpdateHandleIcon(float currentX, float expX, float minX)
    {
        if (handleIconTransform == null) return;

        float progress = Mathf.Abs(currentX - expX) / Mathf.Abs(minX - expX);

        float angle = Mathf.LerpAngle(0, 180f, progress);
        handleIconTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void BeginDrag()
    {
        if (_switchRoutine != null) StopCoroutine(_switchRoutine);

        (_expX, _minX) = GetMinExpView();
        _isDragging = true;
    }

    public void SetDraggedPosition(float newX)
    {
        if (!_isDragging) return;

        float clamped = Mathf.Clamp(newX, Mathf.Min(_minX, _expX), Mathf.Max(_minX, _expX));

        transform.localPosition = new Vector3(clamped, transform.localPosition.y, transform.localPosition.z);

        UpdateHandleIcon(clamped, _expX, _minX);
    }

    public void EndDragSnap()
    {
        if (!_isDragging) return;
        _isDragging = false;

        float distToExp = Mathf.Abs(transform.localPosition.x - _expX);
        float distToMin = Mathf.Abs(transform.localPosition.x - _minX);

        viewMode = distToExp < distToMin ? ViewMode.Expanded : ViewMode.Minimized;

        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = StartCoroutine(SwitchRoutine());
    }

    void SaveState()
    {
        if (!Application.isPlaying || dataStorage == null) return;
        Vector2 payload = new(transform.localPosition.x, (int)viewMode);
        dataStorage.SetValue(payload);
    }

    void LoadState()
    {
        if (dataStorage == null || !DataStorage.hasValue) return;
        Vector2 payload = dataStorage.GetValue<Vector2>();
        if (payload != default)
        {
            viewMode = (ViewMode)(int)payload.y;
            transform.localPosition = new Vector3(payload.x, transform.localPosition.y, transform.localPosition.z);

            (float expX, float minX) = GetMinExpView();
            UpdateHandleIcon(payload.x, expX, minX);

            if (_switchRoutine != null) StopCoroutine(_switchRoutine);
            _switchRoutine = StartCoroutine(SwitchRoutine());
        }
    }
}

public enum ViewMode { Minimized, Expanded }
public enum LerpCurve { Linear, SmoothStep, EaseInQuad, EaseOutQuad, EaseInOutQuad }
public enum ExpDir { FromLeft, FromRight }