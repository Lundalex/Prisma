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

    [SerializeField] RectTransform target;                  // container to resize
    [SerializeField] RectTransform leftTarget;              // element that sits to the left (stays put, auto-fits to its text)
    [SerializeField] RectTransform rightTarget;             // element that sits to the right
    [SerializeField] RectTransform parentContainer;

    [SerializeField] float leftSpacing = 8f;                // gap between leftTarget and target
    [SerializeField] float rightSpacing = 8f;               // gap between target's right edge and rightTarget
    [SerializeField] float padding = 32f;
    [SerializeField] float minWidth = 105f;
    [SerializeField] float maxWidth = 280f;

    [Header("Texts")]
    [TextArea] [SerializeField] string leftStr = "";
    [TextArea] [SerializeField] string placeholderStr = "";
    [TextArea] [SerializeField] string rightStr = "";

    TMP_InputField _input;
    TMP_InputField _leftInput;

#if UNITY_EDITOR
    bool _pendingEditorRebuild;
#endif

    void Reset()
    {
        target = (RectTransform)transform;
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        if (leftTarget != null && leftText == null) leftText = leftTarget.GetComponentInChildren<TMP_Text>();
        if (rightTarget != null && rightText == null) rightText = rightTarget.GetComponentInChildren<TMP_Text>();
    }

    void Start()
    {
        _input = text ? text.GetComponentInParent<TMP_InputField>() : null;
        if (_input) _input.onValueChanged.AddListener(UpdateSize);

        _leftInput = leftText ? leftText.GetComponentInParent<TMP_InputField>() : null;
        if (_leftInput) _leftInput.onValueChanged.AddListener(UpdateLeftSize);

        ApplyInspectorTexts();
        UpdateLeftSize(leftText ? leftText.text : string.Empty);
        UpdateSize(text ? text.text : string.Empty);
    }

    void OnDisable()
    {
        if (_input) _input.onValueChanged.RemoveListener(UpdateSize);
        if (_leftInput) _leftInput.onValueChanged.RemoveListener(UpdateLeftSize);
    }

    void OnValidate()
    {
        ApplyInspectorTexts();
#if UNITY_EDITOR
        if (!Application.isPlaying)
            _pendingEditorRebuild = true;
#endif
    }

    void ApplyInspectorTexts()
    {
        if (leftText && leftStr != null && leftText.text != leftStr)
            leftText.text = leftStr;

        if (rightText && rightStr != null && rightText.text != rightStr)
            rightText.text = rightStr;

        if (placeholderText && placeholderStr != null && placeholderText.text != placeholderStr)
            placeholderText.text = placeholderStr;
    }

    public void UpdateSize(string newValue)
    {
        if (text == null || target == null) return;

        float widthConstraint = (maxWidth > 0f) ? Mathf.Max(1f, maxWidth - padding) : Mathf.Infinity;
        Vector2 pref = text.GetPreferredValues(newValue, widthConstraint, 0f);
        float w = Mathf.Clamp(pref.x + padding, Mathf.Max(1f, minWidth), (maxWidth > 0f ? maxWidth : pref.x + padding));

        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        LayoutRebuilder.ForceRebuildLayoutImmediate(target);

        RepositionForLeftTarget();
        RepositionRightTarget();
        SyncLinkedToTarget();
    }

    // Auto-fit leftTarget to its text
    public void UpdateLeftSize(string newLeftValue)
    {
        if (leftText == null || leftTarget == null)
        {
            RepositionForLeftTarget();
            RepositionRightTarget();
            SyncLinkedToTarget();
            return;
        }

        Vector2 prefL = leftText.GetPreferredValues(newLeftValue, Mathf.Infinity, 0f);
        float lw = Mathf.Max(1f, prefL.x);

        leftTarget.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, lw);
        LayoutRebuilder.ForceRebuildLayoutImmediate(leftTarget);

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

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying && _pendingEditorRebuild)
        {
            _pendingEditorRebuild = false;
            UpdateLeftSize(leftText ? leftText.text : string.Empty);
            UpdateSize(text ? text.text : string.Empty);
        }
    }
#endif
}