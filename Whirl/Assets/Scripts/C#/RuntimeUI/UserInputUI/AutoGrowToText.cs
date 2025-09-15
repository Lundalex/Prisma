using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform)), ExecuteAlways]
public class AutoGrowToText : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] TMP_Text leftText;
    [SerializeField] TMP_Text placeholderText;
    [SerializeField] TMP_Text rightText;

    [SerializeField] RectTransform target;
    [SerializeField] RectTransform leftTarget;
    [SerializeField] RectTransform rightTarget;
    [SerializeField] RectTransform parentContainer;

    [SerializeField] float leftSpacing = 8f;
    [SerializeField] float rightSpacing = 8f;
    [SerializeField] float padding = 32f;
    [SerializeField] float minWidth = 105f;
    [SerializeField] float maxWidth = 280f;

    [Header("Fit To Parent")]
    [SerializeField] bool fitToParentContainer = false;
    [SerializeField] float parentPadding = 0f;

    [Header("Texts")]
    [TextArea] [SerializeField] string leftStr = "";
    [TextArea] [SerializeField] string placeholderStr = "";
    [TextArea] [SerializeField] string rightStr = "";

    // NEW: link to the answer field (optional). When animating, updates are blocked.
    [Header("Answer Field (optional)")]
    [SerializeField] UserAnswerField userAnswerField;

    TMP_InputField _input;
    TMP_InputField _leftInput;

    float _lastParentWidth = float.NaN;
    bool _dirtySize;
    bool _suppressDimCallback;
    bool _suppressTMPCallback;

#if UNITY_EDITOR
    bool _pendingEditorRebuild;
#endif

    void Reset()
    {
        target = (RectTransform)transform;
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        if (leftTarget != null && leftText == null) leftText = leftTarget.GetComponentInChildren<TMP_Text>();
        if (rightTarget != null && rightText == null) rightText = rightTarget.GetComponentInChildren<TMP_Text>();
        if (parentContainer == null && transform.parent != null)
            parentContainer = transform.parent as RectTransform;
    }

    void OnEnable()
    {
        RegisterTMPListeners();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTMPGlobalChanged);
#if UNITY_EDITOR
        if (!Application.isPlaying) _pendingEditorRebuild = true;
#endif
    }

    void OnDisable()
    {
        if (_input) _input.onValueChanged.RemoveListener(UpdateSize);
        if (_leftInput) _leftInput.onValueChanged.RemoveListener(UpdateLeftSize);

        UnregisterTMPListeners();
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTMPGlobalChanged);
    }

    void Start()
    {
        _input = text ? text.GetComponentInParent<TMP_InputField>() : null;
        if (_input) _input.onValueChanged.AddListener(UpdateSize);

        _leftInput = leftText ? leftText.GetComponentInParent<TMP_InputField>() : null;
        if (_leftInput) _leftInput.onValueChanged.AddListener(UpdateLeftSize);

        ApplyInspectorTexts();

        _lastParentWidth = parentContainer ? parentContainer.rect.width : float.NaN;

        RecomputeAllTwice();
    }

    void OnValidate()
    {
        ApplyInspectorTexts();
        UnregisterTMPListeners();
        RegisterTMPListeners();
#if UNITY_EDITOR
        if (!Application.isPlaying) _pendingEditorRebuild = true;
#endif
    }

    void RegisterTMPListeners()
    {
        if (text) text.RegisterDirtyVerticesCallback(OnAnyTMPDirty);
        if (leftText) leftText.RegisterDirtyVerticesCallback(OnAnyTMPDirty);
        if (rightText) rightText.RegisterDirtyVerticesCallback(OnAnyTMPDirty);
        if (placeholderText) placeholderText.RegisterDirtyVerticesCallback(OnAnyTMPDirty);
    }

    void UnregisterTMPListeners()
    {
        if (text) text.UnregisterDirtyVerticesCallback(OnAnyTMPDirty);
        if (leftText) leftText.UnregisterDirtyVerticesCallback(OnAnyTMPDirty);
        if (rightText) rightText.UnregisterDirtyVerticesCallback(OnAnyTMPDirty);
        if (placeholderText) placeholderText.UnregisterDirtyVerticesCallback(OnAnyTMPDirty);
    }

    void OnAnyTMPDirty()
    {
        if (_suppressTMPCallback) return;
        _dirtySize = true; // triggers double pass in LateUpdate
    }

    void ApplyInspectorTexts()
    {
        if (leftText && leftStr != null && leftText.text != leftStr) leftText.text = leftStr;
        if (rightText && rightStr != null && rightText.text != rightStr) rightText.text = rightStr;
        if (placeholderText && placeholderStr != null && placeholderText.text != placeholderStr) placeholderText.text = placeholderStr;
    }

    bool TryGetParentEffectiveMax(out float effectiveMax)
    {
        effectiveMax = 0f;
        if (!fitToParentContainer || parentContainer == null) return false;
        effectiveMax = Mathf.Max(1f, parentContainer.rect.width - parentPadding);
        return true;
    }

    // NEW: simple gate
    bool IsBlockedByAnswerAnim()
    {
        return userAnswerField != null && userAnswerField.IsAnimating;
    }

    public void UpdateSize(string newValue)
    {
        if (IsBlockedByAnswerAnim()) return;
        if (text == null || target == null) return;

        float widthConstraint;
        float upperBound;

        if (TryGetParentEffectiveMax(out float parentMax))
        {
            widthConstraint = Mathf.Max(1f, parentMax - padding);
            upperBound = parentMax;
        }
        else
        {
            widthConstraint = (maxWidth > 0f) ? Mathf.Max(1f, maxWidth - padding) : Mathf.Infinity;
            upperBound = (maxWidth > 0f) ? maxWidth : float.PositiveInfinity;
        }

        Vector2 pref = text.GetPreferredValues(newValue, widthConstraint, 0f);

        float wRaw = pref.x + padding;
        float w = Mathf.Clamp(wRaw, Mathf.Max(1f, minWidth), float.IsInfinity(upperBound) ? wRaw : upperBound);

        _suppressDimCallback = true;
        _suppressTMPCallback = true;
        if (!Mathf.Approximately(target.rect.width, w))
            target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        _suppressTMPCallback = false;
        _suppressDimCallback = false;

        RepositionForLeftTarget();
        RepositionRightTarget();
        SyncLinkedToTarget();
    }

    public void UpdateLeftSize(string newLeftValue)
    {
        if (IsBlockedByAnswerAnim()) return;

        if (leftText == null || leftTarget == null)
        {
            RepositionForLeftTarget();
            RepositionRightTarget();
            SyncLinkedToTarget();
            return;
        }

        Vector2 prefL = leftText.GetPreferredValues(newLeftValue, Mathf.Infinity, 0f);
        float lw = Mathf.Max(1f, prefL.x);

        _suppressDimCallback = true;
        _suppressTMPCallback = true;
        if (!Mathf.Approximately(leftTarget.rect.width, lw))
            leftTarget.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lw);
        LayoutRebuilder.ForceRebuildLayoutImmediate(leftTarget);
        _suppressTMPCallback = false;
        _suppressDimCallback = false;

        RepositionForLeftTarget();
        RepositionRightTarget();
        SyncLinkedToTarget();
    }

    void RepositionForLeftTarget()
    {
        if (leftTarget == null || target == null) return;

        Vector3 leftRightWorld = leftTarget.TransformPoint(new Vector3((1f - leftTarget.pivot.x) * leftTarget.rect.width, 0f, 0f));
        Vector3 worldLeftWithGap = leftRightWorld + Vector3.right * leftSpacing;

        Vector3 pivotOffsetWorld = target.TransformVector(new Vector3(target.pivot.x * target.rect.width, 0f, 0f));
        Vector3 desiredPivotWorld = worldLeftWithGap + pivotOffsetWorld;

        var tp = target.position;
        tp.x = desiredPivotWorld.x;
        target.position = tp;
    }

    void RepositionRightTarget()
    {
        if (rightTarget == null || target == null) return;

        Vector3 targetRightWorld = target.TransformPoint(new Vector3((1f - target.pivot.x) * target.rect.width, 0f, 0f));
        Vector3 worldLeftForRight = targetRightWorld + Vector3.right * rightSpacing;
        Vector3 rightPivotOffset = rightTarget.TransformVector(new Vector3(rightTarget.pivot.x * rightTarget.rect.width, 0f, 0f));
        Vector3 desiredRightPivotWorld = worldLeftForRight + rightPivotOffset;

        var rp = rightTarget.position;
        rp.x = desiredRightPivotWorld.x;
        rightTarget.position = rp;
    }

    void SyncLinkedToTarget()
    {
        if (placeholderText == null || target == null || text == null) return;
        if (placeholderText.rectTransform == null) return;
        placeholderText.rectTransform.position = text.rectTransform.position;
    }

    void LateUpdate()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying && _pendingEditorRebuild)
        {
            _pendingEditorRebuild = false;
            RecomputeAllTwice();
        }
#endif
        if (fitToParentContainer && parentContainer)
        {
            float pw = parentContainer.rect.width;
            if (!Mathf.Approximately(pw, _lastParentWidth))
            {
                _lastParentWidth = pw;
                _dirtySize = true;
            }
        }

        // NEW: block any recompute during answer animations
        if (IsBlockedByAnswerAnim()) return;

        if (_dirtySize)
        {
            _dirtySize = false;
            RecomputeAllTwice();
        }
    }

    void OnTransformParentChanged()
    {
        _lastParentWidth = float.NaN;
        _dirtySize = true;
    }

    void OnRectTransformDimensionsChange()
    {
        if (_suppressDimCallback) return;
        _dirtySize = true;
    }

    // --- double-pass recompute ---
    void RecomputeAllOnce()
    {
        if (IsBlockedByAnswerAnim()) return;
        UpdateLeftSize(leftText ? leftText.text : string.Empty);
        UpdateSize(text ? text.text : string.Empty);
    }

    void RecomputeAllTwice()
    {
        if (IsBlockedByAnswerAnim()) return;

        RecomputeAllOnce();

        text?.ForceMeshUpdate();
        leftText?.ForceMeshUpdate();
        rightText?.ForceMeshUpdate();
        placeholderText?.ForceMeshUpdate();

        Canvas.ForceUpdateCanvases();
        if (target) LayoutRebuilder.ForceRebuildLayoutImmediate(target);
        if (leftTarget) LayoutRebuilder.ForceRebuildLayoutImmediate(leftTarget);
        if (rightTarget) LayoutRebuilder.ForceRebuildLayoutImmediate(rightTarget);

        RecomputeAllOnce();
    }

    // --- listeners that catch "any change" on TMP objects ---
    void OnAnyTMPPreRender(TMP_TextInfo _)
    {
        if (_suppressTMPCallback) return;
        _dirtySize = true; // triggers double pass in LateUpdate
    }

    void OnTMPGlobalChanged(Object changedObj)
    {
        if (_suppressTMPCallback) return;

        if (changedObj == (Object)text ||
            changedObj == (Object)leftText ||
            changedObj == (Object)rightText ||
            changedObj == (Object)placeholderText)
        {
            _dirtySize = true;
        }
    }
}