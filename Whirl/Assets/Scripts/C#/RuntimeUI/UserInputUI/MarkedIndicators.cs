using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class MarkedIndicators : MonoBehaviour
{
    [Header("Appearance")]
    [SerializeField] private Color markedColor = Color.yellow;
    [SerializeField] private Color unmarkedColor = Color.white;

    [Header("Source")]
    [Tooltip("HorizontalSelector that holds the current index to mark.")]
    [SerializeField] private Michsky.MUIP.HorizontalSelector selector;

    [Header("Prev/Next (managed by TaskManager)")]
    [SerializeField] private GameObject prevButton;
    [SerializeField] private GameObject nextButton;

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

    /// <summary>Set Prev/Next visibility.</summary>
    public void SetPrevNext(int currentIndex, int totalCount)
    {
        bool hasPrev = currentIndex > 0;
        bool hasNext = currentIndex < (totalCount - 1);

        if (prevButton != null && prevButton.activeSelf != hasPrev) prevButton.SetActive(hasPrev);
        if (nextButton != null && nextButton.activeSelf != hasNext) nextButton.SetActive(hasNext);
    }

    /// <summary>Marks the indicator corresponding to the selector's current index.</summary>
    public void MarkCurrentIndicator()
    {
        if (selector == null) return;

        int idx = selector.index;
        int count = transform.childCount;
        if (idx < 0 || idx >= count) return;

        SetIndicatorMarked(idx);
    }

    public void SetIndicatorMarked(int index)
    {
        if (TryGetGrandchildImages(index, out var images))
        {
            foreach (var img in images)
                SetRgbPreserveAlpha(img, markedColor);
        }
    }

    public void SetIndicatorUnmarked(int index)
    {
        if (TryGetGrandchildImages(index, out var images))
        {
            foreach (var img in images)
                SetRgbPreserveAlpha(img, unmarkedColor);
        }
    }

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

    public void SetOnlyMarked(int index)
    {
        SetAllUnmarked();
        SetIndicatorMarked(index);
    }

    // ───────────────────────────── helpers ─────────────────────────────

    bool TryGetGrandchildImages(int index, out Image[] images)
    {
        images = null;

        int count = transform.childCount;
        if (index < 0 || index >= count) return false;

        Transform indicator = transform.GetChild(index);
        if (indicator == null) return false;

        var grandchildCount = indicator.childCount;
        System.Collections.Generic.List<Image> list = new System.Collections.Generic.List<Image>();

        for (int i = 0; i < grandchildCount; i++)
        {
            var grandchild = indicator.GetChild(i);
            if (grandchild == null) continue;

            var imgsOnGrandchild = grandchild.GetComponents<Image>();
            if (imgsOnGrandchild != null && imgsOnGrandchild.Length > 0)
                list.AddRange(imgsOnGrandchild);
        }

        if (list.Count == 0) return false;

        images = list.ToArray();
        return true;
    }

    static void SetRgbPreserveAlpha(Image img, Color rgb)
    {
        var c = img.color;
        img.color = new Color(rgb.r, rgb.g, rgb.b, c.a);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying) return;

        if (previewMarkAll && !_previewRunning)
        {
            _previewRunning = true;
            _previewStart = EditorApplication.timeSinceStartup;

            SetAllMarked();
            SceneView.RepaintAll();
        }

        if (_previewRunning)
        {
            double elapsed = EditorApplication.timeSinceStartup - _previewStart;
            if (elapsed >= PreviewDurationSeconds)
            {
                SetAllUnmarked();

                _previewRunning = false;
                previewMarkAll = false;
                SceneView.RepaintAll();
            }
        }
    }
#endif
}