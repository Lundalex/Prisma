using TMPro;
using UnityEngine;

[ExecuteInEditMode]
public class Task : MonoBehaviour
{
    [SerializeField] private string headerText;
    [TextArea(6, 40)] [SerializeField] private string bodyText;
    [SerializeField] private string answerKey;

    [Header("Refs")]
    [SerializeField] private TMP_Text headerTextObj;
    [SerializeField] private TMPBulletListFormatter bodyTextFormatter;
    [SerializeField] private UserMultiLineAnswerField answerField;
    [SerializeField] private WindowToggle windowToggle;

    // Change-detection cache
    [SerializeField, HideInInspector] private int _appliedHash;

    void OnEnable()
    {
        RefreshUI();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        RefreshUI();
    }
#endif

    /// <summary>
    /// Set textual data. Updates UI only when values change.
    /// </summary>
    public void SetData(string header, string body, string answerKey)
    {
        int newHash = ComputeHash(header, body, answerKey);
        if (newHash == _appliedHash) return;

        headerText = header;
        bodyText = body;
        this.answerKey = answerKey;

        RefreshUI();
        _appliedHash = newHash;
    }

    /// <summary>
    /// Window A = MultiLine, Window B = SingleLine.
    /// </summary>
    public void SetWindowByTaskType(bool multiLine_usesA)
    {
        if (windowToggle == null) return;
        windowToggle.SetModeA(multiLine_usesA); // apply immediately (edit & play)
    }

    private void RefreshUI()
    {
        if (headerTextObj != null) headerTextObj.text = headerText ?? string.Empty;
        if (bodyTextFormatter != null) bodyTextFormatter.sourceText = bodyText ?? string.Empty;
        if (answerField != null) answerField.answerKey = answerKey ?? string.Empty;
    }

    private static int ComputeHash(string h, string b, string k)
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 23 + (h != null ? h.GetHashCode() : 0);
            hash = hash * 23 + (b != null ? b.GetHashCode() : 0);
            hash = hash * 23 + (k != null ? k.GetHashCode() : 0);
            return hash;
        }
    }
}