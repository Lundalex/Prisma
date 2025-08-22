using UnityEngine;
using UnityEngine.UI; // for VerticalLayoutGroup
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
public class StreamingMessage : MonoBehaviour
{
    public enum StretchOrigin { Left, Right }

    [Header("References")]
    [Tooltip("Child TextMeshPro (UI) component whose content drives the size.")]
    [SerializeField] private TMP_Text text; // Typically TextMeshProUGUI on a child

    [Header("Target")]
    [Tooltip("The RectTransform to size/anchor. If left empty, falls back to this GameObject's RectTransform.")]
    [SerializeField] private RectTransform targetRT;

    [Header("Content")]
    [Tooltip("Text to display (also settable at runtime via SetText).")]
    [TextArea(2, 6)]
    [SerializeField] private string initialText = "Hello world!";

    [Header("Layout")]
    [Tooltip("Minimum free space to the far parent edge (the edge opposite the stretch origin).")]
    [Min(0f)] [SerializeField] private float minDstToParentEdge = 24f;

    [Tooltip("Padding around the text inside this rect (x = left+right / 2, y = top+bottom / 2).")]
    [SerializeField] private Vector2 padding = new Vector2(8f, 8f);

    [Header("Stretch Origin")]
    [Tooltip("Which parent edge stays fixed as the width changes.")]
    [SerializeField] private StretchOrigin stretchFrom = StretchOrigin.Left;

    RectTransform _parentRT; // Parent of targetRT (for width constraints)
    RectTransform _textRT;
    VerticalLayoutGroup _ownerParentVLG; // VLG on the parent of THIS component's GameObject

    bool _applying;                 // guards re-entrancy during size changes
    int  _fitFramesPending = 0;     // defer fitting to the NEXT frame (edit & play)

    void Awake()
    {
        CacheRefs();
        ApplySettings();
        RequestFit(); // defer
    }

    void Start()
    {
        RequestFit(); // ensure a fit next frame at runtime
    }

    void OnEnable()
    {
        CacheRefs();
        ApplySettings();
        RequestFit(); // defer
    }

    void OnValidate()
    {
        CacheRefs();
        ApplySettings();
        RequestFit(); // defer (avoid resizing in OnValidate)
    }

    void Update()
    {
        if (_fitFramesPending > 0)
        {
            _fitFramesPending--;
            if (_fitFramesPending == 0)
            {
                FitNow();
                FlickParentVerticalLayoutGroup();
            }
        }
    }

    void OnRectTransformDimensionsChange()
    {
        if (_applying) return;  // ignore re-entrant calls caused by our own resizing
        RequestFit();           // defer to next frame
    }

    /// <summary>Set text at runtime and fit immediately (then flick parent VLG).</summary>
    public void SetText(string value)
    {
        if (text == null) return;
        text.text = value ?? string.Empty;
        // Do an immediate fit to avoid one-frame odd visuals, then flick VLG.
        FitImmediate();
    }

    /// <summary>
    /// Public alias for changing the text (handy for UnityEvents / Inspector binding).
    /// Calls SetText under the hood.
    /// </summary>
    public void ChangeText(string value) => SetText(value);

    /// <summary>
    /// Runtime control of which side to stretch from.
    /// </summary>
    public void SetStretchOrigin(StretchOrigin origin)
    {
        stretchFrom = origin;
        FitImmediate();
    }

    /// <summary>
    /// Runtime setter for the target RectTransform.
    /// </summary>
    public void SetTargetRectTransform(RectTransform rt)
    {
        targetRT = rt;
        CacheRefs();
        FitImmediate();
    }

    void RequestFit()
    {
        // schedule exactly one-frame delay
        _fitFramesPending = 1;
    }

    void CacheRefs()
    {
        if (targetRT == null)
            targetRT = GetComponent<RectTransform>(); // fallback if not assigned

        _parentRT = (targetRT != null) ? targetRT.parent as RectTransform : null;
        if (text != null) _textRT = text.rectTransform;

        // Per instruction: find VLG on the parent of THIS component
        var myParent = transform.parent;
        _ownerParentVLG = myParent != null ? myParent.GetComponent<VerticalLayoutGroup>() : null;
    }

    void ApplySettings()
    {
        if (text == null) return;

        // Modern TMP API
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode     = TextOverflowModes.Overflow;

        if (!Application.isPlaying)
            text.text = initialText ?? string.Empty;
    }

    /// <summary>
    /// Forces an immediate measure/layout pass and applies the sizing instantly,
    /// then flicks the parent's VerticalLayoutGroup off/on to update it.
    /// </summary>
    public void FitImmediate()
    {
        if (text == null || targetRT == null) return;

        // Make sure TMP has generated geometry so preferred values are reliable
        text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        // Do our sizing work now (no deferral)
        FitNow();

        // Per instruction: toggle the parent VLG to force it to update
        FlickParentVerticalLayoutGroup();

        Canvas.ForceUpdateCanvases();
    }

    void FlickParentVerticalLayoutGroup()
    {
        if (_ownerParentVLG == null)
        {
            // Try to find again in case hierarchy changed
            var p = transform.parent;
            if (p != null) _ownerParentVLG = p.GetComponent<VerticalLayoutGroup>();
        }

        if (_ownerParentVLG != null)
        {
            // Flick off -> on
            bool wasEnabled = _ownerParentVLG.enabled;
            _ownerParentVLG.enabled = false;
            _ownerParentVLG.enabled = true;

            // Optional: restore previous enabled state if it was originally disabled
            if (!wasEnabled)
                _ownerParentVLG.enabled = false;
        }
    }

    void FitNow()
    {
        if (_applying) return;
        if (targetRT == null || text == null) return;
        if (_textRT == null) _textRT = text.rectTransform;

        _applying = true;

        // Ensure anchoring so we truly stretch from the chosen parent edge.
        if (stretchFrom == StretchOrigin.Left)
        {
            targetRT.anchorMin = new Vector2(0f, targetRT.anchorMin.y);
            targetRT.anchorMax = new Vector2(0f, targetRT.anchorMax.y);
            targetRT.pivot     = new Vector2(0f, targetRT.pivot.y);
            targetRT.anchoredPosition = new Vector2(0f, targetRT.anchoredPosition.y); // flush to parent's left
        }
        else
        {
            targetRT.anchorMin = new Vector2(1f, targetRT.anchorMin.y);
            targetRT.anchorMax = new Vector2(1f, targetRT.anchorMax.y);
            targetRT.pivot     = new Vector2(1f, targetRT.pivot.y);
            targetRT.anchoredPosition = new Vector2(0f, targetRT.anchoredPosition.y); // flush to parent's right
        }

        // Determine dynamic max text width based on parent width and min distance to far edge.
        float parentWidth = (_parentRT != null) ? Mathf.Max(1f, _parentRT.rect.width) : 100000f; // big fallback if no parent
        float padX = Mathf.Max(0f, padding.x) * 2f;
        float padY = Mathf.Max(0f, padding.y) * 2f;

        // max width applies to TEXT area; subtract horizontal padding from available width.
        float maxTextWidth = Mathf.Max(1f, parentWidth - minDstToParentEdge - padX);

        // Compute preferred sizes
        string  content   = text.text ?? string.Empty;
        Vector2 unwrapped = text.GetPreferredValues(content);

        float textW, textH;
        if (unwrapped.x <= maxTextWidth)
        {
            textW = Mathf.Max(1f, unwrapped.x);
            textH = Mathf.Max(1f, unwrapped.y);
        }
        else
        {
            textW = maxTextWidth;
            Vector2 wrapped = text.GetPreferredValues(content, textW, 0f);
            textH = Mathf.Max(1f, wrapped.y);
        }

        float selfW = textW + padX;
        float selfH = textH + padY;

        // Apply sizes to target
        targetRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, selfW);
        targetRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   selfH);

        // Keep text sized & centered inside our box
        _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
        _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   textH);
        _textRT.anchoredPosition = Vector2.zero;

        // (Optional) Previously you matched parent height here. Keep/remove as needed:
        if (_parentRT != null)
            _parentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, selfH);

        if (Application.isPlaying)
            initialText = content;

        _applying = false;
    }
}