using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Animates a RectTransform by driving its left/right/top/bottom offsets.
/// Expanded = offsets -> (0,0) so it fits the parent fully (requires target anchors to stretch).
/// Minimized = target matches the world/global rect outline of another RectTransform ("minimizeTo").
///
/// Notes:
/// - Uses screen-space conversions so it works across Overlay, Camera, and World Space canvases.
/// - X and Y have separate durations/curves; left/right and bottom/top animate together per axis.
/// - Minimize plays the same animation as expand, but reversed (time-reversed curve).
/// - OnExpand/OnMinimize are invoked after a per-action delay counted from when Expand()/Minimize() is called.
///   Tip: set the delay to Mathf.Max(expandDurationX, expandDurationY) to fire when the animation finishes.
/// - NEW: OnEnable snaps instantly to Minimized without firing events.
/// </summary>
public class RectExpandMinimizeController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform target;
    public RectTransform Target { get => target; set => target = value; }

    [Header("Minimize Destination")]
    [Tooltip("When minimizing, the target will animate to match this RectTransform's world-space rect.")]
    [SerializeField] private RectTransform minimizeTo;

    [Tooltip("Use unscaled time for animations and event delays")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Expand / Minimize Animation (shared)")]
    [Tooltip("Duration (seconds) for horizontal (X) expand/minimize.")]
    [SerializeField] private float expandDurationX = 0.35f;
    [Tooltip("Curve for horizontal (X) expand/minimize.")]
    [SerializeField] private AnimationCurve expandCurveX = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Duration (seconds) for vertical (Y) expand/minimize.")]
    [SerializeField] private float expandDurationY = 0.35f;
    [Tooltip("Curve for vertical (Y) expand/minimize.")]
    [SerializeField] private AnimationCurve expandCurveY = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Event Timing")]
    [Tooltip("Delay after calling Expand() before invoking OnExpand.")]
    [SerializeField, Min(0f)] private float expandEventDelay = 0f;
    [Tooltip("Delay after calling Minimize() before invoking OnMinimize.")]
    [SerializeField, Min(0f)] private float minimizeEventDelay = 0f;

    [Header("Events")]
    [Tooltip("Invoked after 'expandEventDelay' from the time Expand() is called.")]
    public UnityEvent OnExpand;
    [Tooltip("Invoked after 'minimizeEventDelay' from the time Minimize() is called.")]
    public UnityEvent OnMinimize;

    // Internal state
    private bool isExpanded = true;
    private Coroutine xRoutine;
    private Coroutine yRoutine;
    private Coroutine stateEventRoutine;

    private void Reset()
    {
        target = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (!target) target = GetComponent<RectTransform>();
    }

    private void Start()
    {
        // Instantly snap to minimized without animation and without triggering events.
        SetInstantNoEvent(expanded: false);
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
        // Play forward
        StartAxisXAnimation(0f, 0f, expandDurationX, expandCurveX, reverse:false);
        StartAxisYAnimation(0f, 0f, expandDurationY, expandCurveY, reverse:false);
        ScheduleStateEvent(expandEventDelay);
    }

    /// <summary>
    /// Animate to minimized = match the world-space rect of 'minimizeTo'.
    /// Uses the SAME durations/curves as expand, but reversed.
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

        // Play same animation as expand, but reversed
        StartAxisXAnimation(leftMargin, rightMargin, expandDurationX, expandCurveX, reverse:true);
        StartAxisYAnimation(bottomMargin, topMargin, expandDurationY, expandCurveY, reverse:true);
        ScheduleStateEvent(minimizeEventDelay);
    }

    /// <summary>Instantly snap to expanded or minimized and schedule the event using the configured delay.</summary>
    public void SetInstant(bool expanded)
    {
        isExpanded = expanded;

        if (expanded)
        {
            SetOffsets(new Vector2(0f, 0f), new Vector2(0f, 0f));
            ScheduleStateEvent(expandEventDelay);
        }
        else
        {
            if (!minimizeTo)
            {
                Debug.LogWarning($"[{nameof(RectExpandMinimizeController)}] SetInstant(false) requested but 'minimizeTo' is not assigned.");
                return;
            }

            if (TryCalculateTargetOffsetsFor(minimizeTo, out Vector2 minOff, out Vector2 maxOff))
            {
                SetOffsets(minOff, maxOff);
                ScheduleStateEvent(minimizeEventDelay);
            }
        }
    }

    /// <summary>
    /// Instantly snap to expanded or minimized WITHOUT triggering any UnityEvents and WITHOUT animation.
    /// Used by OnEnable to start minimized silently.
    /// </summary>
    public void SetInstantNoEvent(bool expanded)
    {
        // Cancel any running animations or pending event invokes
        CancelAnimationsAndPendingEvent();

        isExpanded = expanded;

        if (!target) return;

        if (expanded)
        {
            SetOffsets(Vector2.zero, Vector2.zero);
        }
        else
        {
            if (!minimizeTo)
            {
                Debug.LogWarning($"[{nameof(RectExpandMinimizeController)}] SetInstantNoEvent(false) requested but 'minimizeTo' is not assigned.");
                return;
            }

            if (TryCalculateTargetOffsetsFor(minimizeTo, out Vector2 minOff, out Vector2 maxOff))
            {
                SetOffsets(minOff, maxOff);
            }
        }
    }

    private void CancelAnimationsAndPendingEvent()
    {
        if (xRoutine != null) { StopCoroutine(xRoutine); xRoutine = null; }
        if (yRoutine != null) { StopCoroutine(yRoutine); yRoutine = null; }
        if (stateEventRoutine != null) { StopCoroutine(stateEventRoutine); stateEventRoutine = null; }
    }

    // ---- Axis animation helpers (animate both sides together per axis) ----

    private void StartAxisXAnimation(float targetLeftMargin, float targetRightMargin, float duration, AnimationCurve curve, bool reverse)
    {
        if (xRoutine != null) StopCoroutine(xRoutine);
        float startLeft  = GetLeftMargin();
        float startRight = GetRightMargin();
        xRoutine = StartCoroutine(AnimateAxisX(startLeft, startRight, targetLeftMargin, targetRightMargin,
                                              Mathf.Max(0f, duration), curve ?? AnimationCurve.Linear(0, 0, 1, 1), reverse));
    }

    private void StartAxisYAnimation(float targetBottomMargin, float targetTopMargin, float duration, AnimationCurve curve, bool reverse)
    {
        if (yRoutine != null) StopCoroutine(yRoutine);
        float startBottom = GetBottomMargin();
        float startTop    = GetTopMargin();
        yRoutine = StartCoroutine(AnimateAxisY(startBottom, startTop, targetBottomMargin, targetTopMargin,
                                              Mathf.Max(0f, duration), curve ?? AnimationCurve.Linear(0, 0, 1, 1), reverse));
    }

    private IEnumerator AnimateAxisX(float startLeft, float startRight, float targetLeft, float targetRight, float duration, AnimationCurve curve, bool reverse)
    {
        if (duration <= 0f)
        {
            SetLeftRightMargins(targetLeft, targetRight);
            xRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += DeltaTime() / duration;
            float eased = EvaluateProgress(curve, Mathf.Clamp01(t), reverse);
            float L = Mathf.Lerp(startLeft,  targetLeft,  eased);
            float R = Mathf.Lerp(startRight, targetRight, eased);
            SetLeftRightMargins(L, R);
            yield return null;
        }
        SetLeftRightMargins(targetLeft, targetRight);
        xRoutine = null;
    }

    private IEnumerator AnimateAxisY(float startBottom, float startTop, float targetBottom, float targetTop, float duration, AnimationCurve curve, bool reverse)
    {
        if (duration <= 0f)
        {
            SetBottomTopMargins(targetBottom, targetTop);
            yRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += DeltaTime() / duration;
            float eased = EvaluateProgress(curve, Mathf.Clamp01(t), reverse);
            float B = Mathf.Lerp(startBottom, targetBottom, eased);
            float T = Mathf.Lerp(startTop,    targetTop,    eased);
            SetBottomTopMargins(B, T);
            yield return null;
        }
        SetBottomTopMargins(targetBottom, targetTop);
        yRoutine = null;
    }

    /// <summary>
    /// For reversing, we time-reverse the curve: g(t) = 1 - f(1 - t).
    /// This guarantees minimize feels like a true reverse playback of expand.
    /// </summary>
    private static float EvaluateProgress(AnimationCurve curve, float t, bool reverse)
    {
        t = Mathf.Clamp01(t);
        if (!reverse) return curve.Evaluate(t);
        return 1f - curve.Evaluate(1f - t);
    }

    // ---- Event scheduling ----

    private void ScheduleStateEvent(float delay)
    {
        if (stateEventRoutine != null) StopCoroutine(stateEventRoutine);
        stateEventRoutine = StartCoroutine(InvokeStateEventAfterDelay(delay));
    }

    private IEnumerator InvokeStateEventAfterDelay(float delay)
    {
        if (delay > 0f)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(delay);
            else yield return new WaitForSeconds(delay);
        }

        InvokeStateEvent();
        stateEventRoutine = null;
    }

    private void InvokeStateEvent()
    {
        if (isExpanded) OnExpand?.Invoke();
        else OnMinimize?.Invoke();
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
        Vector2 parentLL = parentLocal[0]; // lower-left
        Vector2 parentUR = parentLocal[2]; // upper-right

        float leftMargin   = left   - parentLL.x;
        float bottomMargin = bottom - parentLL.y;
        float rightMargin  = parentUR.x - right;
        float topMargin    = parentUR.y - top;

        outOffsetMin = new Vector2(leftMargin, bottomMargin);
        outOffsetMax = new Vector2(-rightMargin, -topMargin); // negative margins
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
        if (expandEventDelay < 0f) expandEventDelay = 0f;
        if (minimizeEventDelay < 0f) minimizeEventDelay = 0f;
    }
#endif
}