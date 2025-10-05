using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class ScrollAreaScroller : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [Tooltip("If true, waits a frame so layout/content size changes settle before scrolling.")]
    [SerializeField] private bool useDeferredScroll = true;

    void Reset()
    {
        scrollRect = GetComponent<ScrollRect>();
    }

    /// <summary>
    /// Scroll vertically to a user-friendly normalized value.
    /// 0 = top, 1 = bottom (maps to Unity's 1 = top, 0 = bottom).
    /// </summary>
    public void ScrollTo(float normalized01)
    {
        if (!scrollRect) return;
        normalized01 = Mathf.Clamp01(normalized01);

        if (!gameObject.activeInHierarchy) return;

        if (useDeferredScroll)
            StartCoroutine(ScrollDeferred(normalized01));
        else
            SetNow(normalized01);
    }

    /// <summary>Convenience: scroll all the way down.</summary>
    public void ScrollToBottom() => ScrollTo(1f);

    /// <summary>Convenience: scroll all the way up.</summary>
    public void ScrollToTop() => ScrollTo(0f);

    private void SetNow(float normalized01)
    {
        // Unity uses 1 = top, 0 = bottom; invert to match 1 = bottom.
        float unityValue = 1f - normalized01;

        if (scrollRect.content)
            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);

        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = unityValue;
        Canvas.ForceUpdateCanvases();
    }

    private IEnumerator ScrollDeferred(float normalized01)
    {
        // Let layout rebuild this frame (use WaitForEndOfFrame if you prefer).
        yield return null;
        SetNow(normalized01);
    }
}