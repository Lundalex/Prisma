using TMPro;
using UnityEngine;
using Michsky.MUIP;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class Task : MonoBehaviour
{
    const string TaskManagerTag = "TaskManager";

    [SerializeField] private string headerText;
    [TextArea(6, 40)] [SerializeField] private string bodyText;
    [SerializeField] private string answerKey;

    [Header("Refs")]
    [SerializeField] private TMP_Text headerTextObj;
    [SerializeField] private TMPBulletListFormatter bodyTextFormatter;
    [SerializeField] private UserMultiLineAnswerField answerField;
    [SerializeField] private WindowToggle windowToggle;

    [Header("Single-Line UI")]
    [SerializeField] private AutoGrowToText singleLineAutoGrow;

    [Header("Next Toggles")]
    [SerializeField] private WindowToggle singleLineNextToggle;
    [SerializeField] private WindowToggle multiLineNextToggle;

    [Header("Correct Feedback")]
    public AnimatedPopupIcon singleLineCorrectIcon;
    public AnimatedPopupIcon multiLineCorrectIcon;

    [Header("Manager Link")]
    [SerializeField] private TaskManager taskManager;

    [SerializeField, HideInInspector] private int _appliedHash;
    [SerializeField, HideInInspector] private bool _desiredModeA;

    public bool _isMulti;
    bool _hasRightTaskCache;

    void OnEnable()
    {
        EnsureTaskManagerLinked();
        RefreshUI();
        ApplyWindowToggle();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureTaskManagerLinked();
        RefreshUI();
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (!isActiveAndEnabled) return;
            ApplyWindowToggle();
        };
    }
#endif

    public void SetTaskManager(TaskManager manager) => taskManager = manager;
    public TaskManager TaskManager => taskManager;
    public AutoGrowToText SingleLineAutoGrow => singleLineAutoGrow;

    public virtual void PlayCorrectMark()
    {
        var icon = _isMulti ? multiLineCorrectIcon : singleLineCorrectIcon;
        if (icon == null) icon = multiLineCorrectIcon ?? singleLineCorrectIcon;
        if (icon != null) icon.Play();
    }

    public void GoToPrevTask() { EnsureTaskManagerLinked(); taskManager?.GoToPrevTask(this); }
    public void GoToNextTask() { EnsureTaskManagerLinked(); taskManager?.GoToNextTask(this); }

    public void SetNextToggleByHasRight(bool hasRightTask)
    {
        _hasRightTaskCache = hasRightTask;
        if (singleLineNextToggle != null) singleLineNextToggle.SetModeA(hasRightTask);
        if (multiLineNextToggle != null) multiLineNextToggle.SetModeA(hasRightTask);
    }

    public void ApplyProgressToNextToggles(Verdict v)
    {
        bool showNext = v == Verdict.Success && _hasRightTaskCache;
        if (singleLineNextToggle != null) singleLineNextToggle.SetModeA(showNext);
        if (multiLineNextToggle != null)  multiLineNextToggle.SetModeA(showNext);
    }

    public void SendTip() { EnsureTaskManagerLinked(); taskManager?.SendTip(); }

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

    public void SetPlaceholder(string placeholder)
    {
        if (answerField != null) answerField.SetPlaceholder(placeholder);
        if (singleLineAutoGrow != null) singleLineAutoGrow.SetPlaceholder(placeholder);
    }

    // multiLine_usesA: true => MultiLine active (A), false => SingleLine (B)
    public virtual void SetWindowByTaskType(bool multiLine_usesA)
    {
        _desiredModeA = multiLine_usesA;
        _isMulti = multiLine_usesA;
        ApplyWindowToggle();
    }

    void ApplyWindowToggle()
    {
        if (windowToggle == null) return;
        windowToggle.SetModeA(_desiredModeA);
    }

    void EnsureTaskManagerLinked()
    {
        if (taskManager != null) return;
        var go = GameObject.FindGameObjectWithTag(TaskManagerTag);
        if (go != null) taskManager = go.GetComponent<TaskManager>();
    }

    void RefreshUI()
    {
        if (headerTextObj != null) headerTextObj.text = headerText ?? string.Empty;
        if (bodyTextFormatter != null) bodyTextFormatter.sourceText = bodyText ?? string.Empty;
        if (answerField != null) answerField.answerKey = answerKey ?? string.Empty;
    }

    static int ComputeHash(string h, string b, string k)
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