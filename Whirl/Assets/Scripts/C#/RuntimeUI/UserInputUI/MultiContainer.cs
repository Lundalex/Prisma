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
    [SerializeField] private Transform mainContainerTransform;
    [SerializeField] private Transform handleTransform;
    [SerializeField] private Transform handleIconTransform;

    // Target whose anchors will match the outer container (in world space), accounting for target scale.
    [SerializeField] private RectTransform anchorsTargetTransform;

    [Header("Anchor Offsets (pixels in parent space)")]
    [SerializeField] private float leftOffset = 0f;
    [SerializeField] private float rightOffset = 0f;
    [SerializeField] private float topOffset = 0f;
    [SerializeField] private float bottomOffset = 0f;

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

            UpdatePositionInstant();

            // Keep target anchors matched in the editor (compensates for target scale + applies offsets).
            MatchAnchorsToOuterGlobal();
        }
        else
        {
            Debug.LogWarning("Missing references. Object: MultiContainer");
        }
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

    // === Anchors match, compensating for the target's scale, with edge offsets ===
    private void MatchAnchorsToOuterGlobal()
    {
        if (anchorsTargetTransform == null || outerContainerTransform == null) return;

        RectTransform parent = anchorsTargetTransform.parent as RectTransform;
        if (parent == null) return;

        // Get outer rect corners in parent local space
        Vector3[] worldCorners = new Vector3[4];
        outerContainerTransform.GetWorldCorners(worldCorners); // 0 = BL, 2 = TR

        Vector3 blLocal = parent.worldToLocalMatrix.MultiplyPoint3x4(worldCorners[0]);
        Vector3 trLocal = parent.worldToLocalMatrix.MultiplyPoint3x4(worldCorners[2]);

        // Apply pixel offsets in parent space
        blLocal.x -= leftOffset;
        blLocal.y -= bottomOffset;
        trLocal.x += rightOffset;
        trLocal.y += topOffset;

        Rect pr = parent.rect;
        Vector2 parentBL = new Vector2(-pr.width * parent.pivot.x, -pr.height * parent.pivot.y);
        Vector2 parentSize = pr.size;

        // Initial anchors that frame the (offset) outer rect (ignoring target scale)
        Vector2 anchorMin = new Vector2(
            (blLocal.x - parentBL.x) / parentSize.x,
            (blLocal.y - parentBL.y) / parentSize.y
        );
        Vector2 anchorMax = new Vector2(
            (trLocal.x - parentBL.x) / parentSize.x,
            (trLocal.y - parentBL.y) / parentSize.y
        );

        // Compensate for the target's scale relative to the parent so final world size matches.
        Vector3 parentLossy = parent.lossyScale;
        Vector3 targetLossy = anchorsTargetTransform.lossyScale;

        float sx = Mathf.Abs(parentLossy.x) > 1e-6f ? Mathf.Abs(targetLossy.x / parentLossy.x) : 1f;
        float sy = Mathf.Abs(parentLossy.y) > 1e-6f ? Mathf.Abs(targetLossy.y / parentLossy.y) : 1f;

        // Shrink/grow the anchor rectangle around its center by 1/scale.
        Vector2 center = (anchorMin + anchorMax) * 0.5f;
        Vector2 half   = (anchorMax - anchorMin) * 0.5f;
        Vector2 newHalf = new Vector2(
            half.x / (sx <= 0f ? 1f : sx),
            half.y / (sy <= 0f ? 1f : sy)
        );

        anchorMin = center - newHalf;
        anchorMax = center + newHalf;

        // Clamp to [0,1] to avoid runaway values when offsets push beyond parent.
        anchorMin = new Vector2(Mathf.Clamp01(anchorMin.x), Mathf.Clamp01(anchorMin.y));
        anchorMax = new Vector2(Mathf.Clamp01(anchorMax.x), Mathf.Clamp01(anchorMax.y));

        anchorsTargetTransform.anchorMin = anchorMin;
        anchorsTargetTransform.anchorMax = anchorMax;

        // Zero offsets so the rect fills the (scale-compensated + offset) anchor area.
        anchorsTargetTransform.offsetMin = Vector2.zero;
        anchorsTargetTransform.offsetMax = Vector2.zero;
    }
}

public enum ViewMode { Minimized, Expanded }
public enum LerpCurve { Linear, SmoothStep, EaseInQuad, EaseOutQuad, EaseInOutQuad }
public enum ExpDir { FromLeft, FromRight }