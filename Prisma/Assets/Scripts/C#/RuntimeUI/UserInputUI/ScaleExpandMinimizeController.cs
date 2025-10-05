using System.Collections;
using UnityEngine;

public class ScaleExpandMinimizeController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;
    public Transform Target { get => target; set => target = value; }

    [Tooltip("Use unscaled time for delays and animations")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Scales")]
    [SerializeField] private Vector2 expandedScale  = Vector2.one;
    [SerializeField] private Vector2 minimizedScale = new(0.2f, 0.2f);

    [Header("Animation (shared for Expand & Minimize)")]
    [Tooltip("Duration (seconds) for horizontal (X) scale")]
    [SerializeField] private float durationX = 0.35f;
    [Tooltip("Curve for horizontal (X) scale")]
    [SerializeField] private AnimationCurve curveX = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Duration (seconds) for vertical (Y) scale")]
    [SerializeField] private float durationY = 0.35f;
    [Tooltip("Curve for vertical (Y) scale")]
    [SerializeField] private AnimationCurve curveY = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Delays")]
    [Tooltip("Delay before starting Expand (seconds)")]
    [SerializeField, Min(0f)] private float expandDelay = 0f;
    [Tooltip("Delay before starting Minimize (seconds)")]
    [SerializeField, Min(0f)] private float minimizeDelay = 0f;

    // State
    private bool isExpanded = true;
    private Coroutine scaleRoutine;

    private void Reset()
    {
        target = transform;
    }

    private void Awake()
    {
        if (!target) target = transform;
    }

    /// <summary>Toggle between Expanded and Minimized states.</summary>
    public void ChangeView()
    {
        if (isExpanded) Minimize();
        else Expand();
    }

    /// <summary>Animate to expandedScale.</summary>
    public void Expand()
    {
        isExpanded = true;
        StartScaleAnimation(expandedScale, expandDelay, reverse: false);
    }

    /// <summary>Animate to minimizedScale (reverse time playback of the same curves).</summary>
    public void Minimize()
    {
        isExpanded = false;
        StartScaleAnimation(minimizedScale, minimizeDelay, reverse: true);
    }

    /// <summary>Instantly snap to expanded or minimized without animation.</summary>
    public void SetInstant(bool expanded)
    {
        isExpanded = expanded;
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        SetScale(expanded ? expandedScale : minimizedScale);
    }

    // ---- Animation driver ----

    private void StartScaleAnimation(Vector2 targetScale, float delay, bool reverse)
    {
        if (scaleRoutine != null) StopCoroutine(scaleRoutine);
        scaleRoutine = StartCoroutine(ScaleRoutine(targetScale, Mathf.Max(0f, delay), reverse));
    }

    private IEnumerator ScaleRoutine(Vector2 to, float delay, bool reverse)
    {
        // Optional delay
        if (delay > 0f)
        {
            if (useUnscaledTime) yield return new WaitForSecondsRealtime(delay);
            else yield return new WaitForSeconds(delay);
        }

        if (!target) yield break;

        Vector3 from3 = target.localScale;

        float durX = Mathf.Max(0f, durationX);
        float durY = Mathf.Max(0f, durationY);
        var cX = curveX ?? AnimationCurve.Linear(0, 0, 1, 1);
        var cY = curveY ?? AnimationCurve.Linear(0, 0, 1, 1);

        // Handle zero-length durations cleanly
        if (durX <= 0f && durY <= 0f)
        {
            SetScale(to);
            scaleRoutine = null;
            yield break;
        }

        float tx = 0f, ty = 0f;

        while (tx < 1f || ty < 1f)
        {
            float dt = DeltaTime();

            if (durX > 0f) tx += dt / durX; else tx = 1f;
            if (durY > 0f) ty += dt / durY; else ty = 1f;

            float ex = EvaluateProgress(cX, Mathf.Clamp01(tx), reverse);
            float ey = EvaluateProgress(cY, Mathf.Clamp01(ty), reverse);

            float x = Mathf.Lerp(from3.x, to.x, ex);
            float y = Mathf.Lerp(from3.y, to.y, ey);

            // Keep Z at 1 for UI (or adjust if you need a different convention)
            target.localScale = new Vector3(x, y, 1f);

            yield return null;
        }

        // Snap to exact target at the end
        SetScale(to);
        scaleRoutine = null;
    }

    /// <summary>
    /// Reverse-time playback for minimize: g(t) = 1 - f(1 - t)
    /// </summary>
    private static float EvaluateProgress(AnimationCurve curve, float t, bool reverse)
    {
        t = Mathf.Clamp01(t);
        if (!reverse) return curve.Evaluate(t);
        return 1f - curve.Evaluate(1f - t);
    }

    private void SetScale(Vector2 s)
    {
        if (!target) return;
        target.localScale = new Vector3(s.x, s.y, 1f);
    }

    private float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!target) target = transform;
        if (expandDelay < 0f) expandDelay = 0f;
        if (minimizeDelay < 0f) minimizeDelay = 0f;
    }
#endif
}