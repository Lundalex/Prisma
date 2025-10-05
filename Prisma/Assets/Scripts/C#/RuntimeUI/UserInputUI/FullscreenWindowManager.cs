using System.Collections;
using UnityEngine;

[ExecuteAlways]
public class FullscreenWindowManager : MonoBehaviour
{
    [Header("Children of D")]
    public RectTransform A; // left
    public RectTransform C; // right

    [Header("Middle Variants (B)")]
    public RectTransform B_Tasks;    // middle (tasks)
    public RectTransform B_NoTasks;  // middle (no tasks)
    public bool ShowTasks = true;    // switches between B_Tasks and B_NoTasks
    public GameObject TaskSelector;  // only active when ShowTasks is true

    [Header("Task Side Panel (simulation view)")]
    public bool PreventHideTaskSidePanel = true;
    public GameObject taskSidePanel;

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

    RectTransform ActiveB => ShowTasks ? B_Tasks : B_NoTasks;

    void Reset()
    {
        parentRT = transform as RectTransform;

        // Try auto-assign by common names
        if (!A) A = FindChildRTByName("A");
        if (!C) C = FindChildRTByName("C");
        if (!B_Tasks)   B_Tasks   = FindChildRTByName("B_Tasks");
        if (!B_NoTasks) B_NoTasks = FindChildRTByName("B_NoTasks");

        // Fallbacks
        if (!A && transform.childCount >= 1) A = transform.GetChild(0) as RectTransform;
        if (!C && transform.childCount >= 3) C = transform.GetChild(2) as RectTransform;

        if (normalizeAnchorsOnEnable) NormalizeAnchorsOnce();
        ApplyVariantActiveState();

        currentWidth = Mathf.Max(0f, middleWidth);
        DoLayout();
    }

    void OnEnable()
    {
        parentRT = transform as RectTransform;
        if (normalizeAnchorsOnEnable) NormalizeAnchorsOnce();
        ApplyVariantActiveState();

        if (!Application.isPlaying)
        {
            DoLayout();
            return;
        }

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

        currentWidth = middleWidth;

        // Defer BOTH the active-state toggles and layout to avoid SendMessage during OnValidate
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            ApplyVariantActiveState();
            DoLayout();
        };
    }
#endif

    void OnRectTransformDimensionsChange()
    {
        DoLayout();
    }

    // ---------- Public API ----------

    public void Expand()
    {
        AnimateWidthTo(expandedWidth);
    }

    public void Minimize()
    {
        AnimateWidthTo(minimizedWidth);
    }

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

    public void SetShowTasks(bool show)
    {
        ShowTasks = show;
        ApplyVariantActiveState();
        DoLayout();
    }

    // ---------- Helpers ----------

    void EvaluateSidesState()
    {
        // If both minimized -> expand B; otherwise minimize B
        if (AMinimized && CMinimized) Expand();
        else Minimize();
    }

    void DoLayout()
    {
        if (parentRT == null) parentRT = transform as RectTransform;
        var B = ActiveB;

        if (!parentRT || !A || !C || B == null) return;

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

    // ---------- Variant visibility ----------

    void ApplyVariantActiveState()
    {
        if (B_Tasks && B_Tasks.gameObject.activeSelf != ShowTasks)
            B_Tasks.gameObject.SetActive(ShowTasks);

        if (B_NoTasks && B_NoTasks.gameObject.activeSelf != !ShowTasks)
            B_NoTasks.gameObject.SetActive(!ShowTasks);

        if (TaskSelector && TaskSelector.activeSelf != ShowTasks)
            TaskSelector.SetActive(ShowTasks);

        if (taskSidePanel && taskSidePanel.activeSelf != (ShowTasks || PreventHideTaskSidePanel))
            taskSidePanel.SetActive(ShowTasks || PreventHideTaskSidePanel);
    }

    // ---------- One-time anchor normalization (safe outside Validate/DimChange) ----------

    [ContextMenu("Normalize Anchors Now")]
    void NormalizeAnchorsOnce()
    {
        if (A) CenterHorizontally(A);
        if (B_Tasks) CenterHorizontally(B_Tasks);
        if (B_NoTasks) CenterHorizontally(B_NoTasks);
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

    RectTransform FindChildRTByName(string name)
    {
        for (int i = 0; i < transform.childCount; i++)
        {
            var rt = transform.GetChild(i) as RectTransform;
            if (rt != null && rt.name == name) return rt;
        }
        return null;
    }
}