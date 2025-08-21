using System.Collections;
using UnityEngine;

/// <summary>
/// Animates a RectTransform by driving its left/right/top/bottom offsets.
/// Expanded = offsets -> (0,0) so it fits the parent fully (requires target anchors to stretch).
/// Minimized = target matches the world/global rect outline of another RectTransform ("minimizeTo").
/// 
/// Setup:
/// - Set target anchors to stretch full parent (anchorMin = (0,0), anchorMax = (1,1)) for expand behavior.
/// - Assign "minimizeTo" (any RectTransform in the scene; can be under a different Canvas).
/// 
/// Notes:
/// - Uses screen-space conversions so it works across Screen Space - Overlay, Screen Space - Camera, and World Space canvases.
/// - X and Y have separate durations/curves; left/right (and bottom/top) animate together within each axis.
/// </summary>
public class RectExpandMinimizeController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform target;
    public RectTransform Target { get => target; set => target = value; }

    [Header("Minimize Destination")]
    [Tooltip("When minimizing, the target will animate to match this RectTransform's world-space rect.")]
    [SerializeField] private RectTransform minimizeTo;

    [Tooltip("Use unscaled time for animations")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Expand Animation")]
    [Tooltip("Duration (seconds) for horizontal (X) expand.")]
    [SerializeField] private float expandDurationX = 0.35f;
    [Tooltip("Curve for horizontal (X) expand.")]
    [SerializeField] private AnimationCurve expandCurveX = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Duration (seconds) for vertical (Y) expand.")]
    [SerializeField] private float expandDurationY = 0.35f;
    [Tooltip("Curve for vertical (Y) expand.")]
    [SerializeField] private AnimationCurve expandCurveY = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Minimize Animation")]
    [Tooltip("Duration (seconds) for horizontal (X) minimize.")]
    [SerializeField] private float minimizeDurationX = 0.30f;
    [Tooltip("Curve for horizontal (X) minimize.")]
    [SerializeField] private AnimationCurve minimizeCurveX = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Duration (seconds) for vertical (Y) minimize.")]
    [SerializeField] private float minimizeDurationY = 0.30f;
    [Tooltip("Curve for vertical (Y) minimize.")]
    [SerializeField] private AnimationCurve minimizeCurveY = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // Internal state
    private bool isExpanded = true;
    private Coroutine xRoutine;
    private Coroutine yRoutine;

    private void Reset()
    {
        target = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (!target) target = GetComponent<RectTransform>();
    }

    /// <summary>Toggle between Expanded and Minimized states.</summary>
    public void ChangeView()
    {
        if (isExpanded) Minimize();
        else Expand();
    }

    /// <summary>Animate to expanded (offsets -> 0).</summary>
    public void Expand()
    {
        isExpanded = true;
        StartAxisXAnimation(0f, 0f, expandDurationX, expandCurveX); // left/right margins -> 0
        StartAxisYAnimation(0f, 0f, expandDurationY, expandCurveY); // bottom/top margins -> 0
    }

    /// <summary>
    /// Animate to minimized = match the world-space rect of 'minimizeTo'.
    /// </summary>
    public void Minimize()
    {
        if (!minimizeTo)
        {
            Debug.LogWarning($"[{nameof(RectExpandMinimizeController)}] Minimize requested but 'minimizeTo' is not assigned.");
            return;
        }

        isExpanded = false;

        // Compute margins (left/right/bottom/top) in target's parent space that correspond to minimizeTo's world rect
        if (!TryCalculateTargetOffsetsFor(minimizeTo, out Vector2 targetOffsetMin, out Vector2 targetOffsetMax))
        {
            Debug.LogWarning($"[{nameof(RectExpandMinimizeController)}] Could not calculate minimize offsets (missing parent or canvas?).");
            return;
        }

        // Convert to margins for animation convenience
        float leftMargin   = targetOffsetMin.x;
        float bottomMargin = targetOffsetMin.y;
        float rightMargin  = -targetOffsetMax.x; // offsetMax stores as negative margin
        float topMargin    = -targetOffsetMax.y;

        StartAxisXAnimation(leftMargin, rightMargin, minimizeDurationX, minimizeCurveX);
        StartAxisYAnimation(bottomMargin, topMargin, minimizeDurationY, minimizeCurveY);
    }

    /// <summary>Instantly snap to expanded or minimized without animation.</summary>
    public void SetInstant(bool expanded)
    {
        isExpanded = expanded;

        if (expanded)
        {
            SetOffsets(new Vector2(0f, 0f), new Vector2(0f, 0f));
            return;
        }

        if (!minimizeTo)
        {
            Debug.LogWarning($"[{nameof(RectExpandMinimizeController)}] SetInstant(false) requested but 'minimizeTo' is not assigned.");
            return;
        }

        if (TryCalculateTargetOffsetsFor(minimizeTo, out Vector2 minOff, out Vector2 maxOff))
        {
            SetOffsets(minOff, maxOff);
        }
    }

    // ---- Axis animation helpers (animate both sides together per axis) ----

    private void StartAxisXAnimation(float targetLeftMargin, float targetRightMargin, float duration, AnimationCurve curve)
    {
        if (xRoutine != null) StopCoroutine(xRoutine);
        float startLeft  = GetLeftMargin();
        float startRight = GetRightMargin();
        xRoutine = StartCoroutine(AnimateAxisX(startLeft, startRight, targetLeftMargin, targetRightMargin,
                                              Mathf.Max(0f, duration), curve ?? AnimationCurve.Linear(0, 0, 1, 1)));
    }

    private void StartAxisYAnimation(float targetBottomMargin, float targetTopMargin, float duration, AnimationCurve curve)
    {
        if (yRoutine != null) StopCoroutine(yRoutine);
        float startBottom = GetBottomMargin();
        float startTop    = GetTopMargin();
        yRoutine = StartCoroutine(AnimateAxisY(startBottom, startTop, targetBottomMargin, targetTopMargin,
                                              Mathf.Max(0f, duration), curve ?? AnimationCurve.Linear(0, 0, 1, 1)));
    }

    private IEnumerator AnimateAxisX(float startLeft, float startRight, float targetLeft, float targetRight, float duration, AnimationCurve curve)
    {
        if (duration <= 0f)
        {
            SetLeftRightMargins(targetLeft, targetRight);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += DeltaTime() / duration;
            float eased = curve.Evaluate(Mathf.Clamp01(t));
            float L = Mathf.Lerp(startLeft,  targetLeft,  eased);
            float R = Mathf.Lerp(startRight, targetRight, eased);
            SetLeftRightMargins(L, R);
            yield return null;
        }
        SetLeftRightMargins(targetLeft, targetRight);
        xRoutine = null;
    }

    private IEnumerator AnimateAxisY(float startBottom, float startTop, float targetBottom, float targetTop, float duration, AnimationCurve curve)
    {
        if (duration <= 0f)
        {
            SetBottomTopMargins(targetBottom, targetTop);
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += DeltaTime() / duration;
            float eased = curve.Evaluate(Mathf.Clamp01(t));
            float B = Mathf.Lerp(startBottom, targetBottom, eased);
            float T = Mathf.Lerp(startTop,    targetTop,    eased);
            SetBottomTopMargins(B, T);
            yield return null;
        }
        SetBottomTopMargins(targetBottom, targetTop);
        yRoutine = null;
    }

    // ---- Offset/margin utilities ----

    private void SetOffsets(Vector2 offsetMin, Vector2 offsetMax)
    {
        if (!target) return;
        target.offsetMin = offsetMin;
        target.offsetMax = offsetMax;
    }

    private void SetLeftRightMargins(float left, float right)
    {
        if (!target) return;
        var min = target.offsetMin;
        var max = target.offsetMax;
        min.x = left;      // left margin
        max.x = -right;    // right margin stored as negative
        target.offsetMin = min;
        target.offsetMax = max;
    }

    private void SetBottomTopMargins(float bottom, float top)
    {
        if (!target) return;
        var min = target.offsetMin;
        var max = target.offsetMax;
        min.y = bottom;    // bottom margin
        max.y = -top;      // top margin stored as negative
        target.offsetMin = min;
        target.offsetMax = max;
    }

    private float GetLeftMargin()   => target ? target.offsetMin.x  : 0f;
    private float GetBottomMargin() => target ? target.offsetMin.y  : 0f;
    private float GetRightMargin()  => target ? -target.offsetMax.x : 0f;
    private float GetTopMargin()    => target ? -target.offsetMax.y : 0f;

    // ---- Core math: compute target offsets to match an arbitrary RectTransform's world rect ----

    /// <summary>
    /// Calculates offsetMin/offsetMax for 'target' (relative to its parent) so that the target's rect
    /// matches the world-space rect of 'other'. Works across canvases by converting via screen space.
    /// </summary>
    private bool TryCalculateTargetOffsetsFor(RectTransform other, out Vector2 outOffsetMin, out Vector2 outOffsetMax)
    {
        outOffsetMin = default;
        outOffsetMax = default;

        if (!target) return false;
        var parent = target.parent as RectTransform;
        if (!parent || !other) return false;

        // Get the canvas/camera that drives the parent, for proper screen conversions
        Canvas parentCanvas = parent.GetComponentInParent<Canvas>();
        Camera parentCam = (parentCanvas && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
            ? parentCanvas.worldCamera
            : null;

        // Convert 'other' world corners -> screen -> parent local
        var otherWorld = new Vector3[4];
        other.GetWorldCorners(otherWorld);

        Vector2 otherInParentBL, otherInParentTL, otherInParentTR, otherInParentBR;
        if (!WorldToLocalIn(parent, otherWorld[0], parentCam, out otherInParentBL)) return false; // bottom-left
        if (!WorldToLocalIn(parent, otherWorld[1], parentCam, out otherInParentTL)) return false; // top-left
        if (!WorldToLocalIn(parent, otherWorld[2], parentCam, out otherInParentTR)) return false; // top-right
        if (!WorldToLocalIn(parent, otherWorld[3], parentCam, out otherInParentBR)) return false; // bottom-right

        float left   = Mathf.Min(otherInParentBL.x, otherInParentTL.x, otherInParentTR.x, otherInParentBR.x);
        float right  = Mathf.Max(otherInParentBL.x, otherInParentTL.x, otherInParentTR.x, otherInParentBR.x);
        float bottom = Mathf.Min(otherInParentBL.y, otherInParentTL.y, otherInParentTR.y, otherInParentBR.y);
        float top    = Mathf.Max(otherInParentBL.y, otherInParentTL.y, otherInParentTR.y, otherInParentBR.y);

        // Get parent's local rect corners to compute margins relative to the parent's edges
        var parentLocal = new Vector3[4];
        parent.GetLocalCorners(parentLocal);
        Vector2 parentLL = parentLocal[0]; // lower-left in parent's local space
        Vector2 parentUR = parentLocal[2]; // upper-right

        float leftMargin   = left   - parentLL.x;
        float bottomMargin = bottom - parentLL.y;
        float rightMargin  = parentUR.x - right;
        float topMargin    = parentUR.y - top;

        outOffsetMin = new Vector2(leftMargin, bottomMargin);
        outOffsetMax = new Vector2(-rightMargin, -topMargin); // store as negative margins
        return true;
    }

    private static bool WorldToLocalIn(RectTransform parent, Vector3 world, Camera cam, out Vector2 local)
    {
        Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, world);
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, cam, out local);
    }

    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!target) target = GetComponent<RectTransform>();
    }
#endif
}