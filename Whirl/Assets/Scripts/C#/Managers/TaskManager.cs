using System.Collections.Generic; 
using UnityEngine;
using UnityEngine.Events;
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

    [Header("References")]
    [SerializeField] private DataStorage dataStorage;

    // Windows & selector
    [SerializeField] public WindowManager workspaceWindowManager;
    [SerializeField] public GameObject taskWindowPrefab;
    [SerializeField] public HorizontalSelector taskSelector;
    [SerializeField] public string selectorItemPrefix = "Uppg. ";

    // Optional progress markers (+ Prev/Next owner)
    [SerializeField] public MarkedIndicators markedIndicators;

    // Cached per-task refs
    [SerializeField, HideInInspector] private List<GameObject> taskWindowGOs = new();
    [SerializeField, HideInInspector] private List<Task> taskScripts = new();

    private void OnChanged()
    {
        SetWorkspaceTaskWindows();
        ApplyTaskDataToScripts();
        BuildTaskSelectorItems();
        SyncMarkedIndicators();

        if (markedIndicators != null && taskSelector != null)
            markedIndicators.SetPrevNext(taskSelector.index, tasks.Count);
    }

    // Create/reuse a window named by its index and add it to WindowManager
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
            if (!Application.isPlaying)
                go = (GameObject)PrefabUtility.InstantiatePrefab(taskWindowPrefab, parent);
            else
                go = Instantiate(taskWindowPrefab, parent);
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

        workspaceWindowManager.windows.Add(new WindowManager.WindowItem
        {
            windowName = taskIndex.ToString(),
            windowObject = go,
            buttonObject = null,
            firstSelected = null
        });
    }

    // Keep numeric-named window children in sync with tasks
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
            // text-named -> keep
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

    // Push current UserTask data into each Task component and set its window layout
    private void ApplyTaskDataToScripts()
    {
        int count = tasks.Count;
        for (int i = 0; i < count; i++)
        {
            if (i >= taskScripts.Count) break;
            var view = taskScripts[i];
            if (view == null) continue;

            var t = tasks[i];

            view.SetData(
                header: t.header,
                body: t.body,
                answerKey: t.answerKey
            );

            // Window A = MultiLine, Window B = SingleLine
            bool useA = (t.taskType == TaskType.MultiLine);
            view.SetWindowByTaskType(useA);
        }
    }

    // Build selector items WITHOUT calling MUIP methods that Destroy() in edit mode
    private void BuildTaskSelectorItems()
    {
        if (taskSelector == null) return;

        // Build items and wire selection -> open corresponding window
        var newItems = new List<HorizontalSelector.Item>(tasks.Count);
        for (int i = 0; i < tasks.Count; i++)
        {
            int capturedIndex = i;
            var item = new HorizontalSelector.Item { itemTitle = $"{selectorItemPrefix}{capturedIndex + 1}" };
            item.onItemSelect.AddListener(() =>
            {
                if (workspaceWindowManager != null)
                    workspaceWindowManager.OpenWindowByIndex(capturedIndex);
            });
            newItems.Add(item);
        }

        int prevIndex = Mathf.Clamp(taskSelector.index, 0, Mathf.Max(0, newItems.Count - 1));
        taskSelector.items = newItems;
        taskSelector.defaultIndex = prevIndex;
        taskSelector.index = prevIndex;

        // Keep Prev/Next visibility in sync via MarkedIndicators
        taskSelector.onValueChanged.RemoveAllListeners();
        taskSelector.onValueChanged.AddListener(idx =>
        {
            if (markedIndicators != null)
                markedIndicators.SetPrevNext(idx, tasks.Count);
        });

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // Avoid HorizontalSelector.SetupSelector/UpdateUI in edit mode; they call Destroy().
            // Manually keep label text reasonable and clear any old indicators once.
            if (taskSelector.label != null && taskSelector.items.Count > 0)
                taskSelector.label.text = taskSelector.items[taskSelector.index].itemTitle;
            if (taskSelector.labelHelper != null)
                taskSelector.labelHelper.text = taskSelector.label != null ? taskSelector.label.text : (taskSelector.items.Count > 0 ? taskSelector.items[taskSelector.index].itemTitle : string.Empty);

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
    }

    // Remove all selector indicators immediately (edit & play safe)
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

    // Mark/unmark indicators by task progress
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

    // EXCLUDE CODE BELOW THIS























































    private void Awake()
    {
        UpdateNeighbors();
    }

    private void OnEnable()
    {
        if (dataStorage != null && Application.isPlaying)
        {
            if (DataStorage.hasValue)
            {
                var loaded = dataStorage.GetValue<List<UserTask>>();
                if (loaded != null) tasks = loaded;
            }
        }
        UpdateNeighbors();
    }

    private void OnDestroy()
    {
        if (dataStorage != null && Application.isPlaying)
        {
            dataStorage.SetValue(tasks);
        }
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

    // --- Public API for task progress ---
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
        [ColorUsage(true, true)] public Color normal;
        [ColorUsage(true, true)] public Color edit;
        [ColorUsage(true, true)] public Color success;
        [ColorUsage(true, true)] public Color fail;
        [ColorUsage(true, true)] public Color almost;
    }

    [System.Serializable]
    private struct FeedbackAnimations
    {
        public float shakePixels;
        public float shakeCycleDuration;
        public float shakeCycles;
        public float outlineLerp;
    }

    private enum CorrectIconFeedback { Always, OnlyInWorkspaceView, Never }
    private enum TaskType { SingleLine, MultiLine }

    [System.Serializable]
    private struct AIConditionDescriptions
    {
        [TextArea(2, 40)] public string isCorrect;
        [TextArea(2, 40)] public string isAlmost;
        [TextArea(2, 40)] public string isAlmostFeedback;
        public bool doGiveAlmostFeedback;
    }

    [System.Serializable]
    private struct SingleLineSettings
    {
        public bool caseSensitive;
        public CheckMode checkMode;
        public string leftText;
        public string rightText;
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
        [HideInInspector] public bool hasLeftTask;
        [HideInInspector] public bool hasRightTask;
        [HideInInspector] public Verdict progress;

        public void SetNeighbors(bool hasLeft, bool hasRight)
        {
            hasLeftTask = hasLeft;
            hasRightTask = hasRight;
        }
    }

#if UNITY_EDITOR
    // --- Editor-only: show only selected settings & change detection ---

    [CustomPropertyDrawer(typeof(UserTask))]
    private class UserTaskDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return h;

            float v = EditorGUIUtility.standardVerticalSpacing;

            h += GetHeight(property, "header") + v;
            h += GetHeight(property, "body") + v;
            h += GetHeight(property, "placeholder") + v;
            h += GetHeight(property, "answerKey") + v;
            h += GetHeight(property, "gradingSettings") + v;
            h += GetHeight(property, "aiConditionDescriptions") + v;
            h += GetHeight(property, "taskType") + v;

            var taskTypeProp = property.FindPropertyRelative("taskType");
            if (taskTypeProp != null && (TaskType)taskTypeProp.enumValueIndex == TaskType.SingleLine)
                h += GetHeight(property, "singleLineSettings") + v;
            else
                h += GetHeight(property, "multiLineSettings") + v;

            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            float line = EditorGUIUtility.singleLineHeight;
            float v = EditorGUIUtility.standardVerticalSpacing;

            Rect r = new(position.x, position.y, position.width, line);
            property.isExpanded = EditorGUI.Foldout(r, property.isExpanded, label, true);
            if (!property.isExpanded)
            {
                EditorGUI.EndProperty();
                return;
            }

            EditorGUI.indentLevel++;
            float y = r.y + line + v;

            y = DrawField(position, y, "header", property);
            y = DrawField(position, y, "body", property);
            y = DrawField(position, y, "placeholder", property);
            y = DrawField(position, y, "answerKey", property);
            y = DrawField(position, y, "gradingSettings", property);
            y = DrawField(position, y, "aiConditionDescriptions", property);
            y = DrawField(position, y, "taskType", property);

            var taskTypeProp = property.FindPropertyRelative("taskType");
            if (taskTypeProp != null && (TaskType)taskTypeProp.enumValueIndex == TaskType.SingleLine)
                y = DrawField(position, y, "singleLineSettings", property);
            else
                y = DrawField(position, y, "multiLineSettings", property);

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }

        private static float GetHeight(SerializedProperty root, string rel)
        {
            var p = root.FindPropertyRelative(rel);
            if (p == null) return EditorGUIUtility.singleLineHeight;
            return EditorGUI.GetPropertyHeight(p, true);
        }

        private static float DrawField(Rect pos, float y, string rel, SerializedProperty root)
        {
            var p = root.FindPropertyRelative(rel);
            if (p == null) return y;
            float h = EditorGUI.GetPropertyHeight(p, true);
            Rect r = new(pos.x, y, pos.width, h);
            EditorGUI.PropertyField(r, p, true);
            return y + h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    // --- Editor-only change detection (hashing, runs once per second in play & edit) ---

    private int _lastHash;
    private double _nextCheckTime;

    private void Update()
    {
        EditorHashTick();
    }

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

                // gradingSettings is defined elsewhere; rely on its default GetHashCode
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