using System.Collections;
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class FullscreenWindowManager : MonoBehaviour
{
    [Header("Children of D")]
    public RectTransform A; // left
    public RectTransform B; // middle
    public RectTransform C; // right

    [Header("Widths")]
    [Min(0f)] public float middleWidth = 200f;     // Inspector-editable current width (used as default)
    [Min(0f)] public float minimizedWidth = 120f;  // Target when Minimize() is called
    [Min(0f)] public float expandedWidth  = 420f;  // Target when Expand() is called

    [Header("Animation")]
    [Min(0f)] public float widthAnimDuration = 0.35f;
    public AnimationCurve widthAnimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public bool useUnscaledTime = true;

    [Header("Anchors")]
    [Tooltip("If true, anchors+pivots for A/B/C are centered once on enable/reset. Never changed during Validate/DimChange.")]
    public bool normalizeAnchorsOnEnable = true;

    [Header("Side collapse state")]
    public bool AMinimized; // set via SetAMinimized
    public bool CMinimized; // set via SetCMinimized

    RectTransform parentRT;

    // Runtime width actually used by layout. We animate this.
    float currentWidth;

    // Animation state
    Coroutine widthRoutine;

    void Reset()
    {
        parentRT = transform as RectTransform;

        // auto-assign first three children, if present
        if (!A || !B || !C)
        {
            if (transform.childCount >= 3)
            {
                A = transform.GetChild(0) as RectTransform;
                B = transform.GetChild(1) as RectTransform;
                C = transform.GetChild(2) as RectTransform;
            }
        }

        if (normalizeAnchorsOnEnable) NormalizeAnchorsOnce();
        currentWidth = Mathf.Max(0f, middleWidth);
        DoLayout();
    }

    void OnEnable()
    {
        if (!Application.isPlaying) return;
        parentRT = transform as RectTransform;
        if (normalizeAnchorsOnEnable) NormalizeAnchorsOnce();

        float target = (AMinimized && CMinimized) ? expandedWidth : minimizedWidth;
        currentWidth = target;
        middleWidth = target;
        DoLayout();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Clamp and keep internal width in sync with inspector field
        if (middleWidth < 0f) middleWidth = 0f;
        if (minimizedWidth < 0f) minimizedWidth = 0f;
        if (expandedWidth  < 0f) expandedWidth  = 0f;

        // Keep currentWidth matching what you set in the Inspector (no animation here)
        currentWidth = middleWidth;

        // Defer layout to avoid SendMessage restriction during Validate
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            DoLayout();
        };
    }
#endif

    void OnRectTransformDimensionsChange()
    {
        // Only resize/reposition here; never touch anchors/pivots.
        DoLayout();
    }

    // ---------- Expand / Minimize ----------

    public void Expand()
    {
        AnimateWidthTo(expandedWidth);
    }

    public void Minimize()
    {
        AnimateWidthTo(minimizedWidth);
    }

    // ---------- setters for A/C minimized state ----------

    public void SetAMinimized(bool minimized)
    {
        AMinimized = minimized;
        EvaluateSidesState();
    }

    public void SetCMinimized(bool minimized)
    {
        CMinimized = minimized;
        EvaluateSidesState();
    }

    void EvaluateSidesState()
    {
        // If both minimized -> expand B; otherwise minimize B
        if (AMinimized && CMinimized) Expand();
        else Minimize();
    }

    // ---------- Core layout (positions A/B/C based on currentWidth) ----------

    void DoLayout()
    {
        if (parentRT == null) parentRT = transform as RectTransform;
        if (!parentRT || !A || !B || !C) return;

        float W  = parentRT.rect.width;
        float WB = Mathf.Clamp(currentWidth, 0f, Mathf.Max(0f, W));

        // Middle (B): centered with current width
        B.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, WB);
        B.anchoredPosition = new Vector2(0f, B.anchoredPosition.y);

        // Centers of the side regions [edge <-> B]
        float regionCenterOffset = 0.25f * (W + WB); // (W + WB) / 4

        // Position A and C at centers of their side regions
        A.anchoredPosition = new Vector2(-regionCenterOffset, A.anchoredPosition.y);
        C.anchoredPosition = new Vector2(+regionCenterOffset, C.anchoredPosition.y);
    }

    // ---------- Animation helpers ----------

    void AnimateWidthTo(float targetWidth)
    {
        targetWidth = Mathf.Max(0f, targetWidth);

        // In Edit Mode (not playing), just snap instantly.
        if (!Application.isPlaying)
        {
            currentWidth = targetWidth;
            middleWidth = targetWidth; // reflect in inspector
            DoLayout();
            return;
        }

        if (widthRoutine != null) StopCoroutine(widthRoutine);
        widthRoutine = StartCoroutine(AnimateWidth(currentWidth, targetWidth, Mathf.Max(0f, widthAnimDuration), widthAnimCurve));
    }

    IEnumerator AnimateWidth(float start, float target, float duration, AnimationCurve curve)
    {
        if (duration <= 0f || curve == null)
        {
            currentWidth = target;
            middleWidth = target;
            DoLayout();
            widthRoutine = null;
            yield break;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += DeltaTime() / duration;
            float eased = Mathf.Clamp01(curve.Evaluate(Mathf.Clamp01(t)));
            currentWidth = Mathf.Lerp(start, target, eased);
            DoLayout();
            yield return null;
        }

        currentWidth = target;
        middleWidth = target; // persist the final width
        DoLayout();
        widthRoutine = null;
    }

    float DeltaTime() => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    // ---------- One-time anchor normalization (safe outside Validate/DimChange) ----------

    [ContextMenu("Normalize Anchors Now")]
    void NormalizeAnchorsOnce()
    {
        if (A) CenterHorizontally(A);
        if (B) CenterHorizontally(B);
        if (C) CenterHorizontally(C);
    }

    static void CenterHorizontally(RectTransform rt)
    {
        var min = rt.anchorMin;
        var max = rt.anchorMax;
        rt.anchorMin = new Vector2(0.5f, min.y);
        rt.anchorMax = new Vector2(0.5f, max.y);
        rt.pivot     = new Vector2(0.5f, rt.pivot.y);
    }
}