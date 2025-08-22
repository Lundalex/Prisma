using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class MarkedIndicators : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private Color markedColor = Color.yellow;
    [SerializeField] private Color unmarkedColor = Color.white;

#if UNITY_EDITOR
    [Header("Editor Preview")]
    [Tooltip("Toggle in the editor to mark ALL indicators for 2 seconds, then unmark them.")]
    [SerializeField] private bool previewMarkAll = false;

    bool _previewRunning = false;
    double _previewStart = 0.0;
    const double PreviewDurationSeconds = 2.0;
#endif

    void Start()
    {
        SetAllUnmarked();
    }

    /// <summary>
    /// Sets the color (RGB only, preserves alpha) of ALL Images that are
    /// direct children of the indicator at the given index to markedColor.
    /// Index 0 = first child (top-most in hierarchy).
    /// If index is out of range, logs a warning and does nothing.
    /// </summary>
    public void SetIndicatorMarked(int index)
    {
        if (TryGetGrandchildImages(index, out var images))
        {
            foreach (var img in images)
                SetRgbPreserveAlpha(img, markedColor);
        }
    }

    /// <summary>
    /// Sets the color (RGB only, preserves alpha) of ALL Images that are
    /// direct children of the indicator at the given index to unmarkedColor.
    /// Index 0 = first child (top-most in hierarchy).
    /// If index is out of range, logs a warning and does nothing.
    /// </summary>
    public void SetIndicatorUnmarked(int index)
    {
        if (TryGetGrandchildImages(index, out var images))
        {
            foreach (var img in images)
                SetRgbPreserveAlpha(img, unmarkedColor);
        }
    }

    /// <summary>
    /// Convenience: sets all indicators (all children) to unmarkedColor.
    /// </summary>
    public void SetAllUnmarked()
    {
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            if (TryGetGrandchildImages(i, out var images))
            {
                foreach (var img in images)
                    SetRgbPreserveAlpha(img, unmarkedColor);
            }
        }
    }

    /// <summary>
    /// Convenience: sets all indicators (all children) to markedColor.
    /// </summary>
    public void SetAllMarked()
    {
        int count = transform.childCount;
        for (int i = 0; i < count; i++)
        {
            if (TryGetGrandchildImages(i, out var images))
            {
                foreach (var img in images)
                    SetRgbPreserveAlpha(img, markedColor);
            }
        }
    }

    /// <summary>
    /// Convenience: unmarks all, then marks the specified indicator.
    /// </summary>
    public void SetOnlyMarked(int index)
    {
        SetAllUnmarked();
        SetIndicatorMarked(index);
    }

    // ───────────────────────────── helpers ─────────────────────────────

    // Gets all Image components that are on the DIRECT CHILDREN of the child at 'index'
    // (i.e., grandchildren of this GameObject). Does NOT include the child's own Image,
    // and does not search deeper than one level.
    bool TryGetGrandchildImages(int index, out Image[] images)
    {
        images = null;

        int count = transform.childCount;
        if (index < 0 || index >= count)
        {
            Debug.LogWarning($"{nameof(MarkedIndicators)} ({name}): index {index} is out of range (0..{count - 1}). Call ignored.");
            return false;
        }

        Transform indicator = transform.GetChild(index);
        if (indicator == null)
        {
            Debug.LogWarning($"{nameof(MarkedIndicators)} ({name}): child at index {index} is null. Call ignored.");
            return false;
        }

        var grandchildCount = indicator.childCount;
        System.Collections.Generic.List<Image> list = new System.Collections.Generic.List<Image>();

        for (int i = 0; i < grandchildCount; i++)
        {
            var grandchild = indicator.GetChild(i);
            if (grandchild == null) continue;

            // Only images directly on this grandchild (not deeper)
            var imgsOnGrandchild = grandchild.GetComponents<Image>();
            if (imgsOnGrandchild != null && imgsOnGrandchild.Length > 0)
                list.AddRange(imgsOnGrandchild);
        }

        if (list.Count == 0)
        {
            Debug.LogWarning($"{nameof(MarkedIndicators)} ({name}): no Images found on direct children of '{indicator.name}' (index {index}). Call ignored.");
            return false;
        }

        images = list.ToArray();
        return true;
    }

    // Set only RGB, keep current alpha intact
    static void SetRgbPreserveAlpha(Image img, Color rgb)
    {
        var c = img.color;
        img.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying) return;

        // Start preview when the toggle is turned on
        if (previewMarkAll && !_previewRunning)
        {
            _previewRunning = true;
            _previewStart = EditorApplication.timeSinceStartup;

            SetAllMarked();
            SceneView.RepaintAll();
        }

        // While preview is running, check timer
        if (_previewRunning)
        {
            double elapsed = EditorApplication.timeSinceStartup - _previewStart;
            if (elapsed >= PreviewDurationSeconds)
            {
                SetAllUnmarked();

                _previewRunning = false;
                previewMarkAll = false;   // reset toggle
                SceneView.RepaintAll();
            }
        }
    }
#endif
}