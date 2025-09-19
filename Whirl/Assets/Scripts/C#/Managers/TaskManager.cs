using System.Collections.Generic;
using UnityEngine;
using Michsky.MUIP;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class TaskManager : MonoBehaviour
{
    [SerializeField] private AnswerFieldColors answerFieldColors;
    [SerializeField] private FeedbackAnimations feedbackAnimations;
    [SerializeField] private CorrectIconFeedback correctIconFeedback;
    [SerializeField] private List<UserTask> tasks = new();

    [Header("Main (Workspace) Windows")]
    [SerializeField] public WindowManager workspaceWindowManager;
    [SerializeField] public GameObject taskWindowPrefab;

    [Header("Side Windows")]
    [SerializeField] public WindowManager sideWindowManager;
    [SerializeField] public GameObject sideTaskWindowPrefab;

    [Header("Side Pane Layout")]
    [SerializeField] public DualMultiContainer dualMultiContainer;

    [Header("Selector & Progress")]
    [SerializeField] public HorizontalSelector taskSelector;
    [SerializeField] public string selectorItemPrefix = "Uppg. ";
    [SerializeField] public MarkedIndicators markedIndicators;

    [Header("Persistence")]
    [SerializeField] private DataStorage dataStorage;

    [Header("Chat")]
    [SerializeField] private AssistantChatManager assistantChatManager;

    [Header("Fullscreen")]
    [SerializeField] private GameObject fullscreenView;

    [SerializeField, HideInInspector] private List<GameObject> taskWindowGOs = new();
    [SerializeField, HideInInspector] private List<Task> taskScripts = new();

    [SerializeField, HideInInspector] private List<GameObject> sideTaskWindowGOs = new();
    [SerializeField, HideInInspector] private List<SideTask> sideTaskScripts = new();

    public void OpenFullscreenView()
    {
        if (fullscreenView != null && !fullscreenView.activeSelf)
            fullscreenView.SetActive(true);
    }

    private void OnChanged()
    {
        SetWorkspaceTaskWindows();
        SetSideTaskWindows();
        SyncDualMultiContainerTargets();

        ApplyTaskDataToScripts();
        BuildTaskSelectorItems();
        SyncMarkedIndicators();

        if (markedIndicators != null && taskSelector != null)
            markedIndicators.SetPrevNext(taskSelector.index, tasks.Count);

        if (dualMultiContainer != null && tasks.Count > 0)
        {
            int idx = Mathf.Clamp(taskSelector != null ? taskSelector.index : 0, 0, tasks.Count - 1);
            dualMultiContainer.SetStretchTargetAlt(tasks[idx].taskType == TaskType.SingleLine);
        }
    }

    // ---------- Create windows (MAIN) ----------
    internal void CreateWorkspaceTaskWindow(int taskIndex, Dictionary<int, Transform> existingByIndex)
    {
        if (workspaceWindowManager == null || taskWindowPrefab == null) return;

        var parent = workspaceWindowManager.transform;
        GameObject go;

        if (existingByIndex != null && existingByIndex.TryGetValue(taskIndex, out var t))
        {
            go = t.gameObject;
            go.transform.SetParent(parent, false);
            go.name = taskIndex.ToString();
        }
        else
        {
#if UNITY_EDITOR
            go = !Application.isPlaying
                ? (GameObject)PrefabUtility.InstantiatePrefab(taskWindowPrefab, parent)
                : Instantiate(taskWindowPrefab, parent);
#else
            go = Instantiate(taskWindowPrefab, parent);
#endif
            go.name = taskIndex.ToString();
        }

        if (taskWindowGOs.Count <= taskIndex) taskWindowGOs.Add(go);
        else taskWindowGOs[taskIndex] = go;

        var taskComp = go.GetComponent<Task>();
        if (taskScripts.Count <= taskIndex) taskScripts.Add(taskComp);
        else taskScripts[taskIndex] = taskComp;

        if (taskComp != null) taskComp.SetTaskManager(this);

        workspaceWindowManager.windows.Add(new WindowManager.WindowItem
        {
            windowName = taskIndex.ToString(),
            windowObject = go
        });
    }

    private void SetWorkspaceTaskWindows()
    {
        if (workspaceWindowManager == null) return;

        var parent = workspaceWindowManager.transform;
        var existingByIndex = new Dictionary<int, Transform>();
        var toRemove = new List<GameObject>();

        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (int.TryParse(c.name, out int idx))
            {
                if (idx >= 0 && idx < tasks.Count)
                {
                    if (!existingByIndex.ContainsKey(idx)) existingByIndex[idx] = c;
                    else toRemove.Add(c.gameObject);
                }
                else toRemove.Add(c.gameObject);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(toRemove[i]);
            else Destroy(toRemove[i]);
#else
            Destroy(toRemove[i]);
#endif
        }

        workspaceWindowManager.windows.Clear();

        taskWindowGOs.Clear();
        taskScripts.Clear();
        taskWindowGOs.Capacity = tasks.Count;
        taskScripts.Capacity = tasks.Count;

        for (int i = 0; i < tasks.Count; i++)
            CreateWorkspaceTaskWindow(i, existingByIndex);
    }

    // ---------- Create windows (SIDE) ----------
    internal void CreateSideTaskWindow(int taskIndex, Dictionary<int, Transform> existingByIndex)
    {
        if (sideWindowManager == null || sideTaskWindowPrefab == null) return;

        var parent = sideWindowManager.transform;
        GameObject go;

        if (existingByIndex != null && existingByIndex.TryGetValue(taskIndex, out var t))
        {
            go = t.gameObject;
            go.transform.SetParent(parent, false);
            go.name = taskIndex.ToString();
        }
        else
        {
#if UNITY_EDITOR
            go = !Application.isPlaying
                ? (GameObject)PrefabUtility.InstantiatePrefab(sideTaskWindowPrefab, parent)
                : Instantiate(sideTaskWindowPrefab, parent);
#else
            go = Instantiate(sideTaskWindowPrefab, parent);
#endif
            go.name = taskIndex.ToString();
        }

        if (sideTaskWindowGOs.Count <= taskIndex) sideTaskWindowGOs.Add(go);
        else sideTaskWindowGOs[taskIndex] = go;

        var taskComp = go.GetComponent<SideTask>();
        if (sideTaskScripts.Count <= taskIndex) sideTaskScripts.Add(taskComp);
        else sideTaskScripts[taskIndex] = taskComp;

        if (taskComp != null) taskComp.SetTaskManager(this);

        sideWindowManager.windows.Add(new WindowManager.WindowItem
        {
            windowName = taskIndex.ToString(),
            windowObject = go
        });
    }

    private void SetSideTaskWindows()
    {
        if (sideWindowManager == null) return;

        var parent = sideWindowManager.transform;
        var existingByIndex = new Dictionary<int, Transform>();
        var toRemove = new List<GameObject>();

        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (int.TryParse(c.name, out int idx))
            {
                if (idx >= 0 && idx < tasks.Count)
                {
                    if (!existingByIndex.ContainsKey(idx)) existingByIndex[idx] = c;
                    else toRemove.Add(c.gameObject);
                }
                else toRemove.Add(c.gameObject);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(toRemove[i]);
            else Destroy(toRemove[i]);
#else
            Destroy(toRemove[i]);
#endif
        }

        sideWindowManager.windows.Clear();

        sideTaskWindowGOs.Clear();
        sideTaskScripts.Clear();
        sideTaskWindowGOs.Capacity = tasks.Count;
        sideTaskScripts.Capacity = tasks.Count;

        for (int i = 0; i < tasks.Count; i++)
            CreateSideTaskWindow(i, existingByIndex);
    }

    // ---------- Apply data to Task & SideTask ----------
    private void ApplyTaskDataToScripts()
    {
        int count = tasks.Count;

        // MAIN
        for (int i = 0; i < count; i++)
        {
            if (i >= taskScripts.Count) break;
            var view = taskScripts[i];
            if (view == null) continue;

            var t = tasks[i];

            view.SetData(t.header, t.body, t.answerKey);
            view.SetPlaceholder(t.placeholder);
            view.SetWindowByTaskType(t.taskType == TaskType.MultiLine);
            view.SetNextToggleByHasRight(t.hasRightTask);

            if (i < taskWindowGOs.Count)
            {
                var go = taskWindowGOs[i];
                if (go != null)
                {
                    var fields = go.GetComponentsInChildren<UserAnswerField>(true);
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var f = fields[j];
                        f.ApplyColors(
                            answerFieldColors.normal,
                            answerFieldColors.edit,
                            answerFieldColors.success,
                            answerFieldColors.fail,
                            answerFieldColors.almost
                        );
                        f.ApplyFeedback(
                            feedbackAnimations.shakePixels,
                            feedbackAnimations.shakeCycleDuration,
                            Mathf.RoundToInt(feedbackAnimations.shakeCycles),
                            feedbackAnimations.outlineLerp
                        );
                        f.SetPlaceholder(t.placeholder);
                    }
                }
            }
        }

        // SIDE
        for (int i = 0; i < count; i++)
        {
            if (i >= sideTaskScripts.Count) break;
            var view = sideTaskScripts[i];
            if (view == null) continue;

            var t = tasks[i];

            view.SetData(t.header, t.body, t.answerKey);
            view.SetPlaceholder(t.placeholder);
            view.SetWindowByTaskType(t.taskType == TaskType.MultiLine);
            view.SetNextToggleByHasRight(t.hasRightTask);

            if (i < sideTaskWindowGOs.Count)
            {
                var go = sideTaskWindowGOs[i];
                if (go != null)
                {
                    var fields = go.GetComponentsInChildren<UserAnswerField>(true);
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var f = fields[j];
                        f.ApplyColors(
                            answerFieldColors.normal,
                            answerFieldColors.edit,
                            answerFieldColors.success,
                            answerFieldColors.fail,
                            answerFieldColors.almost
                        );
                        f.ApplyFeedback(
                            feedbackAnimations.shakePixels,
                            feedbackAnimations.shakeCycleDuration,
                            Mathf.RoundToInt(feedbackAnimations.shakeCycles),
                            feedbackAnimations.outlineLerp
                        );
                        f.SetPlaceholder(t.placeholder);
                    }
                }
            }
        }
    }

    // ---------- DualMultiContainer <-> SideTask targets ----------
    private void SyncDualMultiContainerTargets()
    {
        if (dualMultiContainer == null) return;

        int n = tasks.Count;
        var defaults = new RectTransform[n];
        var alts     = new RectTransform[n];

        for (int i = 0; i < n; i++)
        {
            SideTask st = (i < sideTaskScripts.Count) ? sideTaskScripts[i] : null;
            if (st != null)
            {
                defaults[i] = st.singleLineStretchTarget;
                alts[i]     = st.multiLineStretchTarget;
            }
        }

        dualMultiContainer.stretchTargets    = defaults;
        dualMultiContainer.altStretchTargets = alts;

        dualMultiContainer.InitDisplay();
    }

    private void BuildTaskSelectorItems()
    {
        if (taskSelector == null) return;

        var newItems = new List<HorizontalSelector.Item>(tasks.Count);
        for (int i = 0; i < tasks.Count; i++)
        {
            int capturedIndex = i;
            var item = new HorizontalSelector.Item { itemTitle = $"{selectorItemPrefix}{capturedIndex + 1}" };
            item.onItemSelect.AddListener(() =>
            {
                if (workspaceWindowManager != null)
                    workspaceWindowManager.OpenWindowByIndex(capturedIndex);
                if (sideWindowManager != null)
                    sideWindowManager.OpenWindowByIndex(capturedIndex);

                if (dualMultiContainer != null)
                    dualMultiContainer.SetStretchTargetAlt(tasks[capturedIndex].taskType == TaskType.SingleLine);
            });
            newItems.Add(item);
        }

        int prevIndex = Mathf.Clamp(taskSelector.index, 0, Mathf.Max(0, newItems.Count - 1));
        taskSelector.items = newItems;
        taskSelector.defaultIndex = prevIndex;
        taskSelector.index = prevIndex;

        taskSelector.onValueChanged.RemoveAllListeners();
        taskSelector.onValueChanged.AddListener(idx =>
        {
            if (markedIndicators != null)
                markedIndicators.SetPrevNext(idx, tasks.Count);
        });

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (taskSelector.label != null && taskSelector.items.Count > 0)
                taskSelector.label.text = taskSelector.items[taskSelector.index].itemTitle;
            if (taskSelector.labelHelper != null)
                taskSelector.labelHelper.text = taskSelector.label != null ? taskSelector.label.text :
                    (taskSelector.items.Count > 0 ? taskSelector.items[taskSelector.index].itemTitle : string.Empty);
            ClearSelectorIndicatorsImmediate();
        }
        else
        {
            taskSelector.UpdateUI();
        }
#else
        taskSelector.UpdateUI();
#endif

        if (markedIndicators != null)
            markedIndicators.SetPrevNext(prevIndex, tasks.Count);

        if (dualMultiContainer != null && newItems.Count > 0)
            dualMultiContainer.SetStretchTargetAlt(tasks[prevIndex].taskType == TaskType.SingleLine);
    }

    private void ClearSelectorIndicatorsImmediate()
    {
        if (taskSelector == null || taskSelector.indicatorParent == null) return;
        var toKill = new List<GameObject>();
        foreach (Transform child in taskSelector.indicatorParent) toKill.Add(child.gameObject);
#if UNITY_EDITOR
        if (!Application.isPlaying) toKill.ForEach(DestroyImmediate);
        else toKill.ForEach(Destroy);
#else
        toKill.ForEach(Destroy);
#endif
    }

    public void SyncMarkedIndicators()
    {
        if (markedIndicators == null) return;
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].progress == Verdict.Success)
                markedIndicators.SetIndicatorMarked(i);
            else
                markedIndicators.SetIndicatorUnmarked(i);
        }
    }

    // ---------- Progress & feedback ----------
    public void OnAnswerFieldCorrect(Task task)
    {
        if (task == null) return;

        int idx = taskScripts.IndexOf(task);
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx >= 0) SetTaskProgress(idx, Verdict.Success);

        if (correctIconFeedback == CorrectIconFeedback.Never) return;

        bool canPlay = correctIconFeedback == CorrectIconFeedback.Always
            || (correctIconFeedback == CorrectIconFeedback.OnlyInWorkspaceView && task.gameObject.activeInHierarchy);

        if (canPlay) task.PlayCorrectMark();
    }

    public void GoToPrevTask(Task fromTask)
    {
        if (fromTask == null) return;
        int idx = taskScripts.IndexOf(fromTask);
        if (idx < 0 && fromTask is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx <= 0) return;
        OpenTaskByIndex(idx - 1);
    }

    public void GoToNextTask(Task fromTask)
    {
        if (fromTask == null) return;
        int idx = taskScripts.IndexOf(fromTask);
        if (idx < 0 && fromTask is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx < 0 || idx >= tasks.Count - 1) return;
        OpenTaskByIndex(idx + 1);
    }

    public void OpenTaskByIndex(int index)
    {
        if (index < 0 || index >= tasks.Count) return;

        if (workspaceWindowManager != null) workspaceWindowManager.OpenWindowByIndex(index);
        if (sideWindowManager != null) sideWindowManager.OpenWindowByIndex(index);

        if (taskSelector != null) { taskSelector.index = index; taskSelector.UpdateUI(); }
        if (markedIndicators != null) markedIndicators.SetPrevNext(index, tasks.Count);

        if (dualMultiContainer != null)
            dualMultiContainer.SetStretchTargetAlt(tasks[index].taskType == TaskType.SingleLine);
    }

    public void SendTip()
    {
        if (assistantChatManager != null)
            assistantChatManager.SendPresetUserMessage("Tip");
    }

    private void Awake() => UpdateNeighbors();

    private void OnEnable()
    {
        if (dataStorage != null && Application.isPlaying && DataStorage.hasValue)
        {
            var loaded = dataStorage.GetValue<List<UserTask>>();
            if (loaded != null) tasks = loaded;
        }
        UpdateNeighbors();
    }

    private void OnDestroy()
    {
        if (dataStorage != null && Application.isPlaying)
            dataStorage.SetValue(tasks);
    }

    private void UpdateNeighbors()
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            t.SetNeighbors(i > 0, i < tasks.Count - 1);
            tasks[i] = t;
        }
    }

    public void SetTaskProgress(int taskIndex, Verdict progress)
    {
        if (taskIndex < 0 || taskIndex >= tasks.Count) return;
        var t = tasks[taskIndex];
        t.progress = progress;
        tasks[taskIndex] = t;
    }

    public void SetAllTaskProgress(Verdict progress)
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            t.progress = progress;
            tasks[i] = t;
        }
    }

    public void SetTaskProgressByHeader(string header, Verdict progress)
    {
        if (string.IsNullOrEmpty(header)) return;
        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].header == header)
            {
                var t = tasks[i];
                t.progress = progress;
                tasks[i] = t;
                break;
            }
        }
    }

    [System.Serializable]
    private struct AnswerFieldColors
    {
        [ColorUsage(true, true)] public Color normal, edit, success, fail, almost;
    }

    [System.Serializable]
    private struct FeedbackAnimations
    {
        public float shakePixels, shakeCycleDuration, shakeCycles, outlineLerp;
    }

    private enum CorrectIconFeedback { Always, OnlyInWorkspaceView, Never }
    private enum TaskType { SingleLine, MultiLine }

    [System.Serializable]
    private struct AIConditionDescriptions
    {
        [TextArea(2, 40)] public string isCorrect, isAlmost, isAlmostFeedback;
        public bool doGiveAlmostFeedback;
    }

    [System.Serializable]
    private struct SingleLineSettings
    {
        public bool caseSensitive;
        public CheckMode checkMode;
        public string leftText, rightText;
    }

    [System.Serializable]
    private struct MultiLineSettings
    {
        public CheckMode checkMode;
        public bool allowAIThinking;
    }

    [System.Serializable]
    private struct UserTask
    {
        public string header;
        [TextArea(6, 40)] public string body;
        public string placeholder;
        [TextArea(2, 40)] public string answerKey;
        public CommunicationSettings gradingSettings;
        public AIConditionDescriptions aiConditionDescriptions;
        public TaskType taskType;
        public SingleLineSettings singleLineSettings;
        public MultiLineSettings multiLineSettings;
        [HideInInspector] public bool hasLeftTask, hasRightTask;
        [HideInInspector] public Verdict progress;

        public void SetNeighbors(bool hasLeft, bool hasRight)
        {
            hasLeftTask = hasLeft; hasRightTask = hasRight;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(UserTask))]
    private class UserTaskDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return h;

            float v = EditorGUIUtility.standardVerticalSpacing;

            h += GetH(property, "header") + v;
            h += GetH(property, "body") + v;
            h += GetH(property, "placeholder") + v;
            h += GetH(property, "answerKey") + v;
            h += GetH(property, "gradingSettings") + v;
            h += GetH(property, "aiConditionDescriptions") + v;
            h += GetH(property, "taskType") + v;

            var tt = property.FindPropertyRelative("taskType");
            h += GetH(property, tt != null && (TaskType)tt.enumValueIndex == TaskType.SingleLine ? "singleLineSettings" : "multiLineSettings") + v;

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight, v = EditorGUIUtility.standardVerticalSpacing;

            Rect r = new(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, label, true);
            if (!property.isExpanded) { EditorGUI.EndProperty(); return; }

            EditorGUI.indentLevel++;
            float y = r.y + line + v;

            y = Draw(position, y, "header", property);
            y = Draw(position, y, "body", property);
            y = Draw(position, y, "placeholder", property);
            y = Draw(position, y, "answerKey", property);
            y = Draw(position, y, "gradingSettings", property);
            y = Draw(position, y, "aiConditionDescriptions", property);
            y = Draw(position, y, "taskType", property);

            var tt = property.FindPropertyRelative("taskType");
            y = Draw(position, y, tt != null && (TaskType)tt.enumValueIndex == TaskType.SingleLine ? "singleLineSettings" : "multiLineSettings", property);

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static float GetH(SerializedProperty root, string rel)
        {
            var p = root.FindPropertyRelative(rel);
            return p == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(p, true);
        }

        private static float Draw(Rect pos, float y, string rel, SerializedProperty root)
        {
            var p = root.FindPropertyRelative(rel);
            if (p == null) return y;
            float h = EditorGUI.GetPropertyHeight(p, true);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), p, true);
            return y + h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    private int _lastHash;
    private double _nextCheckTime;

    private void Update() => EditorHashTick();

    private void EditorHashTick()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextCheckTime) return;
        _nextCheckTime = now + 1.0;

        int h = ComputeStateHash();
        if (h != _lastHash)
        {
            _lastHash = h;
            OnChanged();
        }
    }

    private int ComputeStateHash()
    {
        unchecked
        {
            int hash = 17;

            hash = hash * 23 + answerFieldColors.normal.GetHashCode();
            hash = hash * 23 + answerFieldColors.edit.GetHashCode();
            hash = hash * 23 + answerFieldColors.success.GetHashCode();
            hash = hash * 23 + answerFieldColors.fail.GetHashCode();
            hash = hash * 23 + answerFieldColors.almost.GetHashCode();

            hash = hash * 23 + feedbackAnimations.shakePixels.GetHashCode();
            hash = hash * 23 + feedbackAnimations.shakeCycleDuration.GetHashCode();
            hash = hash * 23 + feedbackAnimations.shakeCycles.GetHashCode();
            hash = hash * 23 + feedbackAnimations.outlineLerp.GetHashCode();

            hash = hash * 23 + (int)correctIconFeedback;

            hash = hash * 23 + tasks.Count;
            for (int i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                hash = hash * 23 + (t.header != null ? t.header.GetHashCode() : 0);
                hash = hash * 23 + (t.body != null ? t.body.GetHashCode() : 0);
                hash = hash * 23 + (t.placeholder != null ? t.placeholder.GetHashCode() : 0);
                hash = hash * 23 + (t.answerKey != null ? t.answerKey.GetHashCode() : 0);
                hash = hash * 23 + t.gradingSettings.GetHashCode();

                var aic = t.aiConditionDescriptions;
                hash = hash * 23 + (aic.isCorrect != null ? aic.isCorrect.GetHashCode() : 0);
                hash = hash * 23 + (aic.isAlmost != null ? aic.isAlmost.GetHashCode() : 0);
                hash = hash * 23 + (aic.isAlmostFeedback != null ? aic.isAlmostFeedback.GetHashCode() : 0);
                hash = hash * 23 + aic.doGiveAlmostFeedback.GetHashCode();

                hash = hash * 23 + (int)t.taskType;

                var sl = t.singleLineSettings;
                hash = hash * 23 + sl.caseSensitive.GetHashCode();
                hash = hash * 23 + sl.checkMode.GetHashCode();
                hash = hash * 23 + (sl.leftText != null ? sl.leftText.GetHashCode() : 0);
                hash = hash * 23 + (sl.rightText != null ? sl.rightText.GetHashCode() : 0);

                var ml = t.multiLineSettings;
                hash = hash * 23 + ml.checkMode.GetHashCode();
                hash = hash * 23 + ml.allowAIThinking.GetHashCode();

                hash = hash * 23 + t.hasLeftTask.GetHashCode();
                hash = hash * 23 + t.hasRightTask.GetHashCode();
                hash = hash * 23 + t.progress.GetHashCode();
            }

            return hash;
        }
    }
#endif
}