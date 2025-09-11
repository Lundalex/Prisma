using UnityEngine;
using TMPro;

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class AutoGrowTextDownwards : MonoBehaviour
{
    [Header("References")]
    [Tooltip("TextMeshPro component whose height should grow with content.")]
    [SerializeField] private TMP_Text text;

    [Header("Layout")]
    [Tooltip("Extra vertical padding added to the calculated height.")]
    [Min(0f)] [SerializeField] private float verticalPadding = 0f;

    RectTransform _textRT;
    bool _subscribed;
    bool _applying;
    bool _dirty;

    // Snapshot of properties that actually affect the preferred height
    string _lastText;
    float _lastFontSize = -1f;
    TMP_FontAsset _lastFontAsset;
    float _lastLineSpacing = float.NaN;

    void Awake()
    {
        CacheRefs();
        RequestFit();
    }

    void OnEnable()
    {
        CacheRefs();
        SubscribeTMP();
        RequestFit();
    }

    void OnDisable()
    {
        UnsubscribeTMP();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        CacheRefs();
        UnsubscribeTMP();
        SubscribeTMP();
        if (isActiveAndEnabled) RequestFit();
    }
#endif

    void LateUpdate()
    {
        if (_dirty) FitNow();
    }

    // Public API: schedule a re-fit
    public void RequestFit()
    {
        _dirty = true;
    }

    // Public API: immediate fit
    public void FitImmediate()
    {
        _dirty = true;
        FitNow();
    }

    void CacheRefs()
    {
        if (text == null) text = GetComponent<TMP_Text>();
        if (text != null) _textRT = text.rectTransform;
    }

    bool HasRelevantTMPChanges()
    {
        if (text == null) return false;

        // Compare the handful of properties that affect preferred height
        bool changed =
            !string.Equals(_lastText, text.text) ||
            !Mathf.Approximately(_lastFontSize, text.fontSize) ||
            _lastFontAsset != text.font ||
            !Mathf.Approximately(_lastLineSpacing, text.lineSpacing);

        return changed;
    }

    void UpdateSnapshot()
    {
        if (text == null) return;
        _lastText = text.text;
        _lastFontSize = text.fontSize;
        _lastFontAsset = text.font;
        _lastLineSpacing = text.lineSpacing;
    }

    void FitNow()
    {
        if (_applying || text == null || _textRT == null) { _dirty = false; return; }

        _dirty = false;
        _applying = true;

        // Use current width to compute wrapped height
        float currentWidth = Mathf.Max(1f, _textRT.rect.width);
        string content = text.text ?? string.Empty;
        Vector2 preferred = text.GetPreferredValues(content, currentWidth, 0f);

        float newHeight = Mathf.Max(1f, preferred.y + verticalPadding);
        float currentHeight = _textRT.rect.height;

        // Avoid tiny oscillations & needless layout churn
        if (Mathf.Abs(newHeight - currentHeight) > 0.5f)
        {
            _textRT.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, newHeight);
            UnityEngine.UI.LayoutRebuilder.MarkLayoutForRebuild(_textRT);
        }

        // Record the values we just fit against
        UpdateSnapshot();

        _applying = false;
    }

    void OnRectTransformDimensionsChange()
    {
        // Width changes (from parent/layout) need a recompute; guard re-entrancy with _applying
        if (!_applying) RequestFit();
    }

    void SubscribeTMP()
    {
        if (_subscribed) return;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMPTextChanged);
        _subscribed = true;
    }

    void UnsubscribeTMP()
    {
        if (!_subscribed) return;
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMPTextChanged);
        _subscribed = false;
    }

    void OnDestroy()
    {
        UnsubscribeTMP();
    }

    void OnTMPTextChanged(Object obj)
    {
        if (this == null || text == null) return;
        if (obj == (Object)text)
        {
            // Only mark dirty if something that affects height actually changed
            if (HasRelevantTMPChanges())
                RequestFit();
        }
    }
}