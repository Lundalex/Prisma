using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
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
    [SerializeField] private Vector2 padding = new(8f, 8f);

    [Header("Stretch Origin")]
    [Tooltip("Which parent edge stays fixed as the width changes.")]
    [SerializeField] private StretchOrigin stretchFrom = StretchOrigin.Left;

    [Header("Streaming")]
    [Tooltip("If true, SetText will animate the message (type-on). Use SetTextImmediate to bypass.")]
    [SerializeField] private bool streamMessage = false;

    [Tooltip("Characters per second for the type-on animation. Minimum is 1.")]
    [Min(1f)] [SerializeField] private float streamCharsPerSecond = 40f;

    RectTransform _parentRT; 
    RectTransform _textRT;
    VerticalLayoutGroup _ownerParentVLG; 

    bool _applying;                 
    int  _fitFramesPending = 0;
    private const float kTopTextOffsetY = -15f;
    Coroutine _streamCo;            

    // NEW: subscription guard
    bool _subscribedToTMPEvent;

    void Awake()
    {
        CacheRefs();
        ApplySettings();
        SubscribeTMPTextChanged();
        RequestFit(); 
    }

    void OnEnable()
    {
        CacheRefs();
        SubscribeTMPTextChanged();
        RequestFit();
    }

    void OnDisable()
    {
        UnsubscribeTMPTextChanged();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        CacheRefs();
        UnsubscribeTMPTextChanged();
        SubscribeTMPTextChanged();
        if (isActiveAndEnabled)
            RequestFit();
    }
#endif

    void Start()
    {
        RequestFit(); 
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
        if (_applying) return;  
        RequestFit();           
    }

    public void SetText(string value)
    {
        if (text == null) return;
        value ??= string.Empty;

        if (_streamCo != null)
        {
            StopCoroutine(_streamCo);
            _streamCo = null;
        }

        if (Application.isPlaying && streamMessage)
        {
            _streamCo = StartCoroutine(StreamTextRoutine(value));
        }
        else
        {
            text.text = value;
            FitImmediate();
        }
    }

    public void SetTextImmediate(string value)
    {
        if (text == null) return;
        value ??= string.Empty;

        if (_streamCo != null)
        {
            StopCoroutine(_streamCo);
            _streamCo = null;
        }

        text.text = value;
        FitImmediate();
    }

    public void SetStreamOptions(bool enable, float newCharsPerSecond = -1f)
    {
        streamMessage = enable;
        if (newCharsPerSecond > 0f) streamCharsPerSecond = Mathf.Max(1f, newCharsPerSecond);
    }

    public void SetStretchOrigin(StretchOrigin origin)
    {
        stretchFrom = origin;
        FitImmediate();
    }

    public void SetTargetRectTransform(RectTransform rt)
    {
        targetRT = rt;
        CacheRefs();
        FitImmediate();
    }

    void RequestFit()
    {
        _fitFramesPending = 1;
    }

    void CacheRefs()
    {
        if (targetRT == null)
            targetRT = GetComponent<RectTransform>(); 

        _parentRT = (targetRT != null) ? targetRT.parent as RectTransform : null;
        if (text != null) _textRT = text.rectTransform;

        var myParent = transform.parent;
        _ownerParentVLG = myParent != null ? myParent.GetComponent<VerticalLayoutGroup>() : null;
    }

    void ApplySettings()
    {
        if (text == null) return;

        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode     = TextOverflowModes.Overflow;

        if (!Application.isPlaying)
            text.text = initialText ?? string.Empty;
    }

    public void FitImmediate()
    {
        if (text == null || targetRT == null) return;

        text.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        FitNow();

        FlickParentVerticalLayoutGroup();

        Canvas.ForceUpdateCanvases();
    }

    void FlickParentVerticalLayoutGroup()
    {
        if (_ownerParentVLG == null)
        {
            var p = transform.parent;
            if (p != null) _ownerParentVLG = p.GetComponent<VerticalLayoutGroup>();
        }

        if (_ownerParentVLG != null)
        {
            bool wasEnabled = _ownerParentVLG.enabled;
            _ownerParentVLG.enabled = false;
            _ownerParentVLG.enabled = true;
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

        // ─────────────────────────────────────────────────────────────────
        // Horizontal anchoring (unchanged): left/right bubble edge stays fixed
        // ─────────────────────────────────────────────────────────────────
        if (stretchFrom == StretchOrigin.Left)
        {
            targetRT.anchorMin = new Vector2(0f, targetRT.anchorMin.y);
            targetRT.anchorMax = new Vector2(0f, targetRT.anchorMax.y);
            targetRT.pivot     = new Vector2(0f, targetRT.pivot.y);
            targetRT.anchoredPosition = new Vector2(0f, targetRT.anchoredPosition.y);
        }
        else
        {
            targetRT.anchorMin = new Vector2(1f, targetRT.anchorMin.y);
            targetRT.anchorMax = new Vector2(1f, targetRT.anchorMax.y);
            targetRT.pivot     = new Vector2(1f, targetRT.pivot.y);
            targetRT.anchoredPosition = new Vector2(0f, targetRT.anchoredPosition.y);
        }

        targetRT.anchorMin = new Vector2(targetRT.anchorMin.x, 1f);
        targetRT.anchorMax = new Vector2(targetRT.anchorMax.x, 1f);
        targetRT.pivot     = new Vector2(targetRT.pivot.x, 1f);
        targetRT.anchoredPosition = new Vector2(targetRT.anchoredPosition.x, 0f);

        // Do the same for the inner text rect to keep its top edge fixed inside the bubble.
        _textRT.anchorMin = new Vector2(_textRT.anchorMin.x, 1f);
        _textRT.anchorMax = new Vector2(_textRT.anchorMax.x, 1f);
        _textRT.pivot     = new Vector2(_textRT.pivot.x, 1f);

        // ─────────────────────────────────────────────────────────────────
        // Measure and size
        // ─────────────────────────────────────────────────────────────────
        float parentWidth = (_parentRT != null) ? Mathf.Max(1f, _parentRT.rect.width) : 100000f;
        float padX = Mathf.Max(0f, padding.x) * 2f;
        float padY = Mathf.Max(0f, padding.y) * 2f;

        float maxTextWidth = Mathf.Max(1f, parentWidth - minDstToParentEdge - padX);

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

        targetRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, selfW);
        targetRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   selfH);

        _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
        _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   textH);

        // With top pivot, (0,0) means text’s top-left relative to bubble’s pivot/top.
        _textRT.anchoredPosition = new Vector2(0f, kTopTextOffsetY);

        // Preserve previous behavior: set parent height to match (if there is a parent RT)
        if (_parentRT != null)
            _parentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, selfH);

        if (Application.isPlaying)
            initialText = content;

        _applying = false;
    }

    IEnumerator StreamTextRoutine(string fullText)
    {
        var sb = new StringBuilder();
        int i = 0;
        float cps = Mathf.Max(1f, streamCharsPerSecond);
        float secondsPerChar = 1f / cps;

        while (i < fullText.Length)
        {
            // Append whole rich-text tags instantly
            if (fullText[i] == '<')
            {
                int end = fullText.IndexOf('>', i);
                if (end == -1)
                {
                    sb.Append(fullText.Substring(i));
                    i = fullText.Length;
                }
                else
                {
                    sb.Append(fullText, i, end - i + 1);
                    i = end + 1;
                }

                text.text = sb.ToString();
                FitImmediate();
                continue;
            }

            sb.Append(fullText[i]);
            i++;

            text.text = sb.ToString();
            FitImmediate();

            yield return new WaitForSecondsRealtime(secondsPerChar);
        }

        _streamCo = null;
    }

    void SubscribeTMPTextChanged()
    {
        if (_subscribedToTMPEvent) return;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMPTextChanged);
        _subscribedToTMPEvent = true;
    }

    void UnsubscribeTMPTextChanged()
    {
        if (!_subscribedToTMPEvent) return;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMPTextChanged);
        _subscribedToTMPEvent = false;
    }

    void OnDestroy()
    {
        UnsubscribeTMPTextChanged();
    }

    void OnTMPTextChanged(Object obj)
    {
        if (this == null || text == null) return;

        if (obj == (Object)text)
            RequestFit();
    }
}