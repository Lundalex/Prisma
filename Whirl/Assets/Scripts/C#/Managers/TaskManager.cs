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
    
    [Header("References")]
    [SerializeField] private DataStorage dataStorage;
    [SerializeField] private WindowManager workspaceWindowManager;

    private void OnChanged()
    {
        
    }

































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