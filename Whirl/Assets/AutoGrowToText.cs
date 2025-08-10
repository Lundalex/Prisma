using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(RectTransform))]
public class AutoGrowToText : MonoBehaviour
{
    [SerializeField] TMP_Text text;
    [SerializeField] RectTransform target;           // container to resize
    [SerializeField] Vector2 padding = new(16, 8);
    [SerializeField] float maxWidth = 0f;            // 0 -> unlimited
    [SerializeField] float minWidth = 0f;
    [SerializeField] float minHeight = 0f;

    void Reset()
    {
        target = (RectTransform)transform;
        if (text == null) text = GetComponentInChildren<TMP_Text>();
    }

    void OnEnable()
    {
        TryHookInputField(true);
        UpdateSize();
    }

    void OnDisable() => TryHookInputField(false);

    void TryHookInputField(bool add)
    {
        var input = text.GetComponentInParent<TMP_InputField>();
        if (input == null) return;
        if (add) input.onValueChanged.AddListener(_ => UpdateSize());
        else     input.onValueChanged.RemoveAllListeners();
    }

    public void UpdateSize()
    {
        if (text == null || target == null) return;
        text.ForceMeshUpdate();

        float widthConstraint = (maxWidth > 0f) ? Mathf.Max(1f, maxWidth - padding.x) : Mathf.Infinity;
        Vector2 pref = text.GetPreferredValues(text.text, widthConstraint, 0f);

        float w = Mathf.Clamp(pref.x + padding.x, Mathf.Max(1f, minWidth), (maxWidth > 0f ? maxWidth : pref.x + padding.x));
        float h = Mathf.Max(pref.y + padding.y, minHeight);

        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, w);
        target.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   h);

        LayoutRebuilder.ForceRebuildLayoutImmediate(target);
    }
}