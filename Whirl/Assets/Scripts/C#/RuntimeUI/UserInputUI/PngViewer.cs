using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;

[ExecuteAlways]
public class PngViewer : MonoBehaviour
{
    [Header("Settings")]
    [Range(0f, 1f)] public float movableWindowSize = 1f;
    [Range(0.01f, 1f)] public float scrollZoomSpeed = 0.1f;
    public Color color = Color.white;

    [Header("Contents")]
    public Sprite[] sprites;

    [Header("References")]
    public RectTransform fullWindow;
    public RectTransform dragArea;
    public RectTransform outerRect;
    public WindowDragger windowDragger;

    [Header("Prefabs")]
    public GameObject spriteImagePrefab;

    [Header("Visibility")]
    public CanvasGroup canvasGroup;
    public float fadeInSeconds = 0.25f;

    [Header("Debug")]
    public bool forceUpdate;

    const float padding = 20f;

    float _lastFullW, _lastFullH;
    bool _fading;
    bool _enabled = false;
    float _fadeT;
    Coroutine _fadeRoutine;

    public void EnableAndSetViewedImages(Sprite[] input_sprites)
    {
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        sprites = input_sprites;
        RecalculateLayout();
        _enabled = true;
    }

    public void Disable()
    {
        if (!_enabled) return;
        _enabled = false;
        _fading = false;

        if (!Application.isPlaying)
        {
            if (canvasGroup) canvasGroup.alpha = 0f;
            if (gameObject.activeSelf) gameObject.SetActive(false);
            return;
        }

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeOutThenDisable());
    }

    System.Collections.IEnumerator FadeOutThenDisable()
    {
        float d = Mathf.Max(0.0001f, fadeInSeconds);
        float t = 0f;
        float start = canvasGroup ? canvasGroup.alpha : 1f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / d);
            if (canvasGroup) canvasGroup.alpha = Mathf.Lerp(start, 0f, k);
            yield return null;
        }

        if (canvasGroup) canvasGroup.alpha = 0f;
        _fadeRoutine = null;
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    void OnEnable()
    {
        _fadeT = 0f;
        _fading = true;
        _enabled = true;
        if (canvasGroup) canvasGroup.alpha = 0f;
        _lastFullW = fullWindow ? fullWindow.rect.width : 0f;
        _lastFullH = fullWindow ? fullWindow.rect.height : 0f;
    }

    void OnDisable()
    {
        _fading = false;
        if (_fadeRoutine != null) { StopCoroutine(_fadeRoutine); _fadeRoutine = null; }
        if (canvasGroup) canvasGroup.alpha = 0f;
    }

    void Update()
    {
        HandleScrollZoom();

        if (forceUpdate)
        {
            forceUpdate = false;
            RecalculateLayout();
        }

        CheckFullWindowResize();
        TickFadeIn();
    }

    void TickFadeIn()
    {
        if (!canvasGroup || !_fading) return;
        float d = Mathf.Max(0.0001f, fadeInSeconds);
        _fadeT += Application.isPlaying ? Time.unscaledDeltaTime : 0f;
        canvasGroup.alpha = Mathf.Clamp01(_fadeT / d);
        if (canvasGroup.alpha >= 1f)
        {
            _fading = false;
            ApplySizing(ComputeRatio());
        }
    }

    void CheckFullWindowResize()
    {
        if (!fullWindow || !outerRect || !dragArea || sprites == null || sprites.Length == 0) return;
        float w = fullWindow.rect.width;
        float h = fullWindow.rect.height;
        if (Mathf.Abs(w - _lastFullW) > 0.01f || Mathf.Abs(h - _lastFullH) > 0.01f)
        {
            _lastFullW = w; _lastFullH = h;
            ApplySizing(ComputeRatio());
            if (windowDragger) windowDragger.Reclamp();
        }
    }

    void HandleScrollZoom()
    {
        if (!Application.isPlaying) return;
        float scroll = Input.mouseScrollDelta.y;
        if (scroll == 0f) return;

        movableWindowSize = Mathf.Clamp01(movableWindowSize + scroll * scrollZoomSpeed);
        ApplySizing(ComputeRatio());
        if (windowDragger) windowDragger.Reclamp();
    }

    void RecalculateLayout()
    {
        if (!dragArea || !outerRect || !fullWindow || sprites == null || sprites.Length == 0 || !spriteImagePrefab) return;

        RebuildContent();
        movableWindowSize = 1f;
        ApplySizing(ComputeRatio());
        JumpToTop();
        if (windowDragger) windowDragger.Reclamp();
    }

    void RebuildContent()
    {
        for (int i = outerRect.childCount - 1; i >= 0; i--)
        {
            var c = outerRect.GetChild(i);
            if (Application.isPlaying) Destroy(c.gameObject);
            else DestroyImmediate(c.gameObject);
        }

        for (int i = 0; i < sprites.Length; i++)
        {
            var go = Instantiate(spriteImagePrefab, outerRect);
            var img = go.GetComponent<Image>();
            img.sprite = sprites[i];
            img.color = color;
        }
    }

    float ComputeRatio()
    {
        float totH = -padding + 10 + 10; // top+bottom
        float maxW = 0f;
        for (int i = 0; i < sprites.Length; i++)
        {
            var s = sprites[i];
            totH += s.rect.height + padding;
            if (s.rect.width > maxW) maxW = s.rect.width;
        }
        if (maxW <= 0f) maxW = 1f;
        float r = totH / maxW;
        if (float.IsNaN(r) || float.IsInfinity(r) || r <= 0f) r = 1f;
        return r;
    }

    void ApplySizing(float ratio)
    {
        if (float.IsNaN(ratio) || float.IsInfinity(ratio) || ratio <= 0f) ratio = 1f;
        if (!fullWindow || !outerRect || !dragArea) return;

        float fullW = fullWindow.rect.width;
        float fullH = fullWindow.rect.height;
        if (!IsFinite(fullW) || !IsFinite(fullH)) return;

        float wMin = fullH / ratio;
        float wMax = fullW;
        float width = Mathf.Lerp(wMin, wMax, Mathf.Clamp01(movableWindowSize));
        float height = width * ratio;

        if (!IsFinite(width) || !IsFinite(height)) return;

        outerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        outerRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);

        float overflow = height - fullH;
        if (!IsFinite(overflow)) overflow = 0f;

        var min = dragArea.offsetMin;
        var max = dragArea.offsetMax;
        min.y = -overflow;
        max.y = overflow;
        dragArea.offsetMin = min;
        dragArea.offsetMax = max;
    }

    void JumpToTop()
    {
        if (!outerRect || !fullWindow) return;

        float fullH = fullWindow.rect.height;
        float contentH = outerRect.rect.height;
        if (!IsFinite(fullH) || !IsFinite(contentH)) return;

        float overflow = Mathf.Max(0f, contentH - fullH);
        var pos = outerRect.anchoredPosition;
        pos.x = 0f;
        pos.y = -overflow;            // safe "top" instead of a huge magic number
        if (!IsFinite(pos.y)) pos.y = 0f;

        outerRect.anchoredPosition = pos;
        if (windowDragger) windowDragger.Reclamp();
    }

    static bool IsFinite(float v) => !(float.IsNaN(v) || float.IsInfinity(v));
}