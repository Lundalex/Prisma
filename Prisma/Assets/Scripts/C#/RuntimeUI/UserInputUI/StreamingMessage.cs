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
    [SerializeField] private TMP_Text text;

    [Header("Targets")]
    [Tooltip("RectTransforms to size/anchor. If unassigned, they are ignored.")]
    [SerializeField] private RectTransform targetRT_A;
    [SerializeField] private RectTransform targetRT_B;

    [Header("Content")]
    [Tooltip("Text to display (also settable at runtime via SetText).")]
    [TextArea(2, 6)]
    [SerializeField] private string initialText = "Hello world!";

    [Header("Layout")]
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

    RectTransform _parentRT_A;
    RectTransform _parentRT_B;
    RectTransform _textRT;
    VerticalLayoutGroup _ownerParentVLG;

    bool _applying;
    int  _fitFramesPending = 0;
    private const float kTopTextOffsetY = -17f;
    Coroutine _streamCo;

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

    public void SetTargets(RectTransform a, RectTransform b)
    {
        targetRT_A = a;
        targetRT_B = b;
        CacheRefs();
        FitImmediate();
    }

    void RequestFit()
    {
        _fitFramesPending = 1;
    }

    void CacheRefs()
    {
        // No defaulting: if A/B are null, we simply ignore them elsewhere.
        _parentRT_A = (targetRT_A != null) ? targetRT_A.parent as RectTransform : null;
        _parentRT_B = (targetRT_B != null) ? targetRT_B.parent as RectTransform : null;

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
        if (text == null) return;
        if (targetRT_A == null && targetRT_B == null) return;

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
        if (text == null) return;
        if (targetRT_A == null && targetRT_B == null) return;
        if (_textRT == null) _textRT = text.rectTransform;

        _applying = true;

        // Top-anchored text rect (fixed top edge inside bubble)
        _textRT.anchorMin = new Vector2(_textRT.anchorMin.x, 1f);
        _textRT.anchorMax = new Vector2(_textRT.anchorMax.x, 1f);
        _textRT.pivot     = new Vector2(_textRT.pivot.x, 1f);

        // Horizontal anchoring helper for a target
        void ApplyAnchors(RectTransform rt)
        {
            if (rt == null) return;

            if (stretchFrom == StretchOrigin.Left)
            {
                rt.anchorMin = new Vector2(0f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(0f, rt.anchorMax.y);
                rt.pivot     = new Vector2(0f, rt.pivot.y);
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }
            else
            {
                rt.anchorMin = new Vector2(1f, rt.anchorMin.y);
                rt.anchorMax = new Vector2(1f, rt.anchorMax.y);
                rt.pivot     = new Vector2(1f, rt.pivot.y);
                rt.anchoredPosition = new Vector2(0f, rt.anchoredPosition.y);
            }

            rt.anchorMin = new Vector2(rt.anchorMin.x, 1f);
            rt.anchorMax = new Vector2(rt.anchorMax.x, 1f);
            rt.pivot     = new Vector2(rt.pivot.x, 1f);
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 0f);
        }

        ApplyAnchors(targetRT_A);
        ApplyAnchors(targetRT_B);

        // Measure and size (use the tightest constraint among assigned parents)
        float parentWidthA = (_parentRT_A != null) ? Mathf.Max(1f, _parentRT_A.rect.width) : float.PositiveInfinity;
        float parentWidthB = (_parentRT_B != null) ? Mathf.Max(1f, _parentRT_B.rect.width) : float.PositiveInfinity;
        float parentWidth  = Mathf.Min(parentWidthA, parentWidthB);

        if (float.IsInfinity(parentWidth) && (targetRT_A == null || _parentRT_A == null) && (targetRT_B == null || _parentRT_B == null))
        {
            // No usable parent constraints; do nothing.
            _applying = false;
            return;
        }

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

        void SizeTarget(RectTransform rt, RectTransform parentRT)
        {
            if (rt == null) return;
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, selfW);
            rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   selfH);

            // Keep previous behavior: if target has a parent RT, match height
            if (parentRT != null)
                parentRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, selfH);
        }

        SizeTarget(targetRT_A, _parentRT_A);
        SizeTarget(targetRT_B, _parentRT_B);

        _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textW);
        _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   textH);
        _textRT.anchoredPosition = new Vector2(0f, kTopTextOffsetY);

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