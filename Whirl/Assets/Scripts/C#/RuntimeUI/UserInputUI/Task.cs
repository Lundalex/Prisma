using TMPro;
using UnityEngine;
using Michsky.MUIP;

[ExecuteInEditMode]
public class Task : MonoBehaviour
{
    private const string TaskManagerTag = "TaskManager";

    [SerializeField] private string headerText;
    [TextArea(6, 40)] [SerializeField] private string bodyText;
    [SerializeField] private string answerKey;

    [Header("Refs")]
    [SerializeField] private TMP_Text headerTextObj;
    [SerializeField] private TMPBulletListFormatter bodyTextFormatter;
    [SerializeField] private UserMultiLineAnswerField answerField;
    [SerializeField] private WindowToggle windowToggle;

    [Header("Next Toggles")]
    [SerializeField] private WindowToggle singleLineNextToggle;
    [SerializeField] private WindowToggle multiLineNextToggle;

    [Header("Correct Feedback")]
    [SerializeField] private AnimatedPopupIcon correctMark;

    [Header("Manager Link")]
    [SerializeField] private TaskManager taskManager;

    [SerializeField, HideInInspector] private int _appliedHash;

    void OnEnable()
    {
        EnsureTaskManagerLinked();
        RefreshUI();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureTaskManagerLinked();
        RefreshUI();
    }
#endif

    public void SetTaskManager(TaskManager manager) => taskManager = manager;
    public TaskManager TaskManager => taskManager;

    public void PlayCorrectMark()
    {
        if (correctMark != null) correctMark.Play();
    }

    public void GoToPrevTask()
    {
        EnsureTaskManagerLinked();
        taskManager?.GoToPrevTask(this);
    }

    public void GoToNextTask()
    {
        EnsureTaskManagerLinked();
        taskManager?.GoToNextTask(this);
    }

    public void SetNextToggleByHasRight(bool hasRightTask)
    {
        bool aActive = hasRightTask;
        if (singleLineNextToggle != null) singleLineNextToggle.SetModeA(aActive);
        if (multiLineNextToggle != null)  multiLineNextToggle.SetModeA(aActive);
    }

    public void SendTip()
    {
        EnsureTaskManagerLinked();
        taskManager?.SendTip();
    }

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
    }

    public void SetWindowByTaskType(bool multiLine_usesA)
    {
        if (windowToggle == null) return;
        windowToggle.SetModeA(multiLine_usesA);
    }

    private void EnsureTaskManagerLinked()
    {
        if (taskManager != null) return;
        var go = GameObject.FindGameObjectWithTag(TaskManagerTag);
        if (go != null) taskManager = go.GetComponent<TaskManager>();
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