using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Resources2;
using Michsky.MUIP;
using Utilities.Extensions;

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
    public float handleBaseScale = 1f;
    public float hoverScaleMultiplier = 1.6f;
    public float hoverDuration = .12f;
    public LerpCurve hoverCurve = LerpCurve.SmoothStep;

    [Header("Handle (Minimized Display)")]
    [SerializeField] private Transform minimizedHandleTransform;
    [SerializeField] private float minimizedHandleScaleMultiplier = 1.4f;

    [Header("Icon Tuning")]
    [Tooltip("Multiplier applied to the REGULAR handle icon scales (used to fine-tune visual size).")]
    public float regularIconScaleFactor = 0.8f;

    [Header("Tooltips")]
    [SerializeField] private TooltipContent minimizedTooltip;
    [SerializeField] private TooltipContent expandedTooltip;
    [SerializeField] private GameObject minimizedTooltipR;
    [SerializeField] private GameObject expandedTooltipR;

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

    [Header("Stretch Targets (Default)")]
    [SerializeField] public RectTransform[] stretchTargets;   // <— now an array

    [Header("Stretch Target Offsets")]
    [SerializeField] protected float leftOffset = 0f;
    [SerializeField] protected float rightOffset = 0f;
    [SerializeField] protected float topOffset = 0f;
    [SerializeField] protected float bottomOffset = 0f;

    private Main main;
    private Coroutine _switchRoutine;
    private Coroutine _transitionRoutine;
    private bool _isDragging;
    private bool _isHovering;
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
            handleTransform.rotation = expandDir == ExpDir.FromRight ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.Euler(0f, 0f, 0f);

            UpdatePositionInstant();
            ApplyIconsImmediate();
            UpdateTooltips();
            MatchAnchorsToOuterGlobal(); // <- updates all targets
        }
        else
        {
            Debug.LogWarning("Missing references. Object: MultiContainer");
        }
    }

    public void SwitchViewMode()
    {
        viewMode = viewMode == ViewMode.Minimized ? ViewMode.Expanded : ViewMode.Minimized;

        if (!Application.isPlaying)
        {
            UpdatePositionInstant();
            ApplyIconsImmediate();
            UpdateTooltips();
            return;
        }

        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = StartCoroutine(SwitchRoutine());
    }

    private float EvalMoveCurve(float t)  => Lerp.Evaluate((Lerp.Curve)lerpCurve, t);
    private float EvalHoverCurve(float t) => Lerp.Evaluate((Lerp.Curve)hoverCurve, t);
    private static float EaseInOut01(float t)
    {
        t = Mathf.Clamp01(t);
        return t * t * (3f - 2f * t); // smoothstep
    }

    private IEnumerator SwitchRoutine()
    {
        (float expX, float minX) = GetMinExpView();
        float startX  = transform.localPosition.x;
        float targetX = viewMode == ViewMode.Expanded ? expX : minX;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, lerpDuration);
            float e = EvalMoveCurve(t);
            float x = Mathf.Lerp(startX, targetX, e);

            transform.localPosition = new Vector3(x, transform.localPosition.y);
            UpdateHandleIconRotation(x, expX, minX);
            yield return null;
        }

        transform.localPosition = new Vector3(targetX, transform.localPosition.y, transform.localPosition.z);
        UpdateHandleIconRotation(targetX, expX, minX);
        _switchRoutine = null;

        UpdateTooltips();
        ApplyStateAnimated();
    }

    private void UpdatePositionInstant()
    {
        (float expView, float minView) = GetMinExpView();
        float x = viewMode == ViewMode.Expanded ? expView : minView;
        transform.localPosition = new Vector3(x, transform.localPosition.y);
        UpdateHandleIconRotation(x, expView, minView);
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

    private void UpdateHandleIconRotation(float currentX, float expX, float minX)
    {
        if (handleIconTransform == null) return;
        float progress = Mathf.Abs(currentX - expX) / Mathf.Abs(minX - expX);
        float angle = Mathf.LerpAngle(0, 180f, progress);
        handleIconTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    // ===== Drag / Snap =====
    public void BeginDrag()
    {
        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        (_expX, _minX) = GetMinExpView();
        _isDragging = true;
        OnHandlePointerEnter(); // treat drag as hover
    }

    public void SetDraggedPosition(float newX)
    {
        if (!_isDragging) return;
        float clamped = Mathf.Clamp(newX, Mathf.Min(_minX, _expX), Mathf.Max(_minX, _expX));
        transform.localPosition = new Vector3(clamped, transform.localPosition.y, transform.localPosition.z);
        UpdateHandleIconRotation(clamped, _expX, _minX);
    }

    public void EndDragSnap()
    {
        if (!_isDragging) return;
        _isDragging = false;

        float distToExp = Mathf.Abs(transform.localPosition.x - _expX);
        float distToMin = Mathf.Abs(transform.localPosition.x - _minX);
        viewMode = distToExp < distToMin ? ViewMode.Expanded : ViewMode.Minimized;

        OnHandlePointerExit();
        if (_switchRoutine != null) StopCoroutine(_switchRoutine);
        _switchRoutine = StartCoroutine(SwitchRoutine());
    }

    // ===== Hover (called by DragHandle) =====
    public void OnHandlePointerEnter()
    {
        _isHovering = true;
        ApplyStateAnimated();
    }

    public void OnHandlePointerExit()
    {
        _isHovering = false;
        ApplyStateAnimated();
    }

    // ===== Icon/Handle transitions =====

    private (float mainIcon, float minIcon, float handle) ComputeTargets()
    {
        bool hasMinIcon = minimizedHandleTransform != null;
        bool minIconActive = hasMinIcon && !_isHovering && viewMode == ViewMode.Minimized;

        float mainIconTarget = minIconActive ? 0f : 1f;
        float minIconTarget  = minIconActive ? 1f : 0f;

        float handleTarget = handleBaseScale * (_isHovering ? hoverScaleMultiplier : 1f);
        if (minIconActive) handleTarget *= Mathf.Max(0f, minimizedHandleScaleMultiplier);

        return (mainIconTarget, minIconTarget, handleTarget);
    }

    private void ApplyIconsImmediate()
    {
        var t = ComputeTargets();
        SetIconScales(t.mainIcon, t.minIcon);
        SetHandleScale(t.handle);
    }

    private void ApplyStateAnimated()
    {
        if (!Application.isPlaying) { ApplyIconsImmediate(); return; }
        var t = ComputeTargets();
        StartIconAndHandleTransition(t.mainIcon, t.minIcon, t.handle);
    }

    private void StartIconAndHandleTransition(float targetMain, float targetMin, float targetHandleScale)
    {
        if (_transitionRoutine != null) StopCoroutine(_transitionRoutine);
        _transitionRoutine = StartCoroutine(IconAndHandleTransitionRoutine(targetMain, targetMin, targetHandleScale));
    }

    private IEnumerator IconAndHandleTransitionRoutine(float targetMain, float targetMin, float targetHandleScale)
    {
        if (handleTransform == null) yield break;

        float main0Vis = handleIconTransform != null ? handleIconTransform.localScale.x : 1f * regularIconScaleFactor;
        float min0Vis  = minimizedHandleTransform != null ? minimizedHandleTransform.localScale.x : 0f;
        float hs0      = handleTransform.localScale.x;

        float targetMainVis = targetMain * regularIconScaleFactor;
        float targetMinVis  = targetMin;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, hoverDuration);
            float th = EvalHoverCurve(Mathf.Clamp01(t));
            float ti = EaseInOut01(th);

            float mainSVis = Mathf.Lerp(main0Vis, targetMainVis, ti);
            float minSVis  = Mathf.Lerp(min0Vis,  targetMinVis,  ti);

            if (handleIconTransform != null)
                handleIconTransform.localScale = new Vector3(mainSVis, mainSVis, 1f);
            if (minimizedHandleTransform != null)
                minimizedHandleTransform.localScale = new Vector3(minSVis, minSVis, 1f);

            float hS = Mathf.Lerp(hs0, targetHandleScale, th);
            handleTransform.localScale = new Vector3(hS, hS, 1f);

            yield return null;
        }

        SetIconScales(targetMain, targetMin);
        SetHandleScale(targetHandleScale);
        _transitionRoutine = null;
    }

    private void SetIconScales(float mainIconScale, float minimizedIconScale)
    {
        if (handleIconTransform != null)
        {
            float vis = mainIconScale * regularIconScaleFactor;
            handleIconTransform.localScale = new Vector3(vis, vis, 1f);
        }
        if (minimizedHandleTransform != null)
        {
            minimizedHandleTransform.localScale = new Vector3(minimizedIconScale, minimizedIconScale, 1f);
        }
    }

    private void SetHandleScale(float s)
    {
        if (handleTransform != null)
            handleTransform.localScale = new Vector3(s, s, 1f);
    }

    // ===== Tooltips =====
    private void UpdateTooltips()
    {
        if (expandedTooltip && expandedTooltipR)
        {
            expandedTooltipR.SetActive(viewMode == ViewMode.Expanded);
            expandedTooltip.enabled = viewMode == ViewMode.Expanded;
        }
        if (minimizedTooltip && minimizedTooltipR)
        {
            minimizedTooltip.enabled = viewMode == ViewMode.Minimized;
            minimizedTooltipR.SetActive(viewMode == ViewMode.Minimized);
        }
    }

    // ===== Persist =====
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
            UpdateHandleIconRotation(payload.x, expX, minX);

            ApplyIconsImmediate();
            UpdateTooltips();
        }
    }

    // ===== Stretching =====
    protected virtual void MatchAnchorsToOuterGlobal()
    {
        if (outerContainerTransform == null || stretchTargets == null) return;
        for (int i = 0; i < stretchTargets.Length; i++)
        {
            var t = stretchTargets[i];
            if (t == null) continue;
            UpdateAnchorForTarget(t, leftOffset, rightOffset, topOffset, bottomOffset);
        }
    }

    // Shared anchor-update routine so subclasses can reuse with alt offsets/targets
    protected void UpdateAnchorForTarget(RectTransform target, float left, float right, float top, float bottom)
    {
        if (target == null || outerContainerTransform == null) return;

        RectTransform parent = target.parent as RectTransform;
        if (parent == null) return;

        Vector3[] worldCorners = new Vector3[4];
        outerContainerTransform.GetWorldCorners(worldCorners); // 0 = BL, 2 = TR

        Vector3 blLocal = parent.worldToLocalMatrix.MultiplyPoint3x4(worldCorners[0]);
        Vector3 trLocal = parent.worldToLocalMatrix.MultiplyPoint3x4(worldCorners[2]);

        blLocal.x -= left;    blLocal.y -= bottom;
        trLocal.x += right;   trLocal.y += top;

        Rect pr = parent.rect;
        Vector2 parentBL = new(-pr.width * parent.pivot.x, -pr.height * parent.pivot.y);
        Vector2 parentSize = pr.size;

        Vector2 anchorMin = new(
            (blLocal.x - parentBL.x) / parentSize.x,
            (blLocal.y - parentBL.y) / parentSize.y
        );
        Vector2 anchorMax = new(
            (trLocal.x - parentBL.x) / parentSize.x,
            (trLocal.y - parentBL.y) / parentSize.y
        );

        Vector3 parentLossy = parent.lossyScale;
        Vector3 targetLossy = target.lossyScale;

        float sx = Mathf.Abs(parentLossy.x) > 1e-6f ? Mathf.Abs(targetLossy.x / parentLossy.x) : 1f;
        float sy = Mathf.Abs(parentLossy.y) > 1e-6f ? Mathf.Abs(targetLossy.y / parentLossy.y) : 1f;

        Vector2 center = (anchorMin + anchorMax) * 0.5f;
        Vector2 half   = (anchorMax - anchorMin) * 0.5f;
        Vector2 newHalf = new(half.x / (sx <= 0f ? 1f : sx), half.y / (sy <= 0f ? 1f : sy));

        anchorMin = center - newHalf;
        anchorMax = center + newHalf;

        anchorMin = new Vector2(Mathf.Clamp01(anchorMin.x), Mathf.Clamp01(anchorMin.y));
        anchorMax = new Vector2(Mathf.Clamp01(anchorMax.x), Mathf.Clamp01(anchorMax.y));

        target.anchorMin = anchorMin;
        target.anchorMax = anchorMax;
        target.offsetMin = Vector2.zero;
        target.offsetMax = Vector2.zero;
    }
}

public enum ViewMode { Minimized, Expanded }
public enum LerpCurve { Linear, SmoothStep, EaseInQuad, EaseOutQuad, EaseInOutQuad }
public enum ExpDir { FromLeft, FromRight }