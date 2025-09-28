using System.Collections; 
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
    [SerializeField] private AIConditionDescriptions aiConditionDescriptions;

    [Header("Layout")]
    [Tooltip("Workspace padding = Global single-line parent padding + this extra value.")]
    public float workspacePaddingExtra = 145f;
    [Tooltip("Global base padding used for single-line inputs. Single-part tasks and parts may override this with their own setting.")]
    public float parentPaddingSideGlobal = 0f;
    [SerializeField] private List<UserTask> tasks = new();

    [Header("--- REFERENCES ---")]

    [Header("Main (Workspace) Windows")]
    public WindowManager workspaceWindowManager;
    public GameObject taskWindowPrefab;

    [Header("Side Windows")]
    public WindowManager sideWindowManager;
    public GameObject sideTaskWindowPrefab;

    [Header("Side Pane Layout")]
    public DualMultiContainer dualMultiContainer;

    [Header("Viewers")]
    public PngViewer pngViewer;

    [System.Serializable]
    public struct SelectorGroup
    {
        public HorizontalSelector selector;
        public MarkedIndicators indicators;
    }

    [Header("Selectors & Progress")]
    [Tooltip("Each element pairs a selector with its own indicators.")]
    public SelectorGroup[] selectorGroups;
    public string selectorItemPrefix = "Uppg. ";

    [Header("Persistence")]
    [Tooltip("Used to persist ONLY task progress between scene reloads during play (resets between plays).")]
    [SerializeField] private DataStorage progressStorage;

    [Tooltip("Used to persist ONLY the CURRENT task index between scene reloads during play (resets between plays).")]
    [SerializeField] private DataStorage selectedIndexStorage;

    [Tooltip("Used to persist ONLY the TIP/ASK state (\"Tip\"/solution visibility flags) between scene reloads during play (resets between plays).")]
    [SerializeField] private DataStorage tipShownStorage;

    [Header("Chat")]
    [SerializeField] private AssistantChatManager assistantChatManager;

    [Header("Fullscreen")]
    [SerializeField] private GameObject fullscreenView;

    [Header("Life Cycle")]
    [SerializeField] private ProgramLifeCycleManager lifeCycleManager;

    [Header("Config Activation")]
    public ConfigHelper configHelper;
    public string targetCollectionName = "Tasks";

    // Runtime-set references
    private SceneManagementHeader sceneManagementHeader;

    [SerializeField, HideInInspector] private List<GameObject> taskWindowGOs = new();
    [SerializeField, HideInInspector] private List<Task> taskScripts = new();
    [SerializeField, HideInInspector] private List<GameObject> sideTaskWindowGOs = new();
    [SerializeField, HideInInspector] private List<SideTask> sideTaskScripts = new();

    struct FlatRef { public int task; public int part; }
    [SerializeField, HideInInspector] private List<FlatRef> _flat = new();
    int FlatCount => _flat.Count;

    // Tip state: when true for a flat index, show Solution UI even if not solved
    [SerializeField, HideInInspector] private List<bool> _tipShown = new();

    int _currentIndex;
    bool _didRuntimeInit;
    Coroutine _consistencyLoop;

    RectTransform[] _activeDefaults;
    RectTransform[] _activeAlts;

    bool IsPart(FlatRef r) => r.part >= 0;

    UserTask GetTaskRef(int taskIndex) => tasks[taskIndex];

    UserTaskPart GetPart(in UserTask t, int partIndex) => (t.parts != null && partIndex >= 0 && partIndex < t.parts.Count)
        ? t.parts[partIndex]
        : default;

    Verdict GetItemProgress(int flatIndex)
    {
        var r = _flat[flatIndex];
        var t = tasks[r.task];
        if (IsPart(r))
        {
            var p = t.parts[r.part];
            return p.progress;
        }
        return t.progress;
    }

    void SetItemProgressInternal(int flatIndex, Verdict progress)
    {
        var r = _flat[flatIndex];
        var t = tasks[r.task];
        if (IsPart(r))
        {
            var p = t.parts[r.part];
            p.progress = progress;
            t.parts[r.part] = p;
        }
        else
        {
            t.progress = progress;
        }
        tasks[r.task] = t;
    }

    public void OpenSolutionViewerFor(Task task)
    {
        if (!sceneManagementHeader)
        {
            var hdrGO = GameObject.FindGameObjectWithTag("SceneManagementHeader");
            if (hdrGO != null) sceneManagementHeader = hdrGO.GetComponent<SceneManagementHeader>();
        }
        if (sceneManagementHeader != null) sceneManagementHeader.SetFullscreenState(true);

        if (pngViewer == null || task == null) return;
        int idx = taskScripts.IndexOf(task);
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx < 0 || idx >= _flat.Count) return;

        int parentTaskIndex = _flat[idx].task;
        var sprites = tasks[parentTaskIndex].solutionSprites;
        pngViewer.EnableAndSetViewedImages(sprites);
    }

    // Ask/Solution control for both main & side
    public void OpenAskWindowsFor(Task task)
    {
        if (task == null) return;

        int idx = taskScripts.IndexOf(task);
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx < 0) return;

        var main = (idx >= 0 && idx < taskScripts.Count) ? taskScripts[idx] : null;
        var side = (idx >= 0 && idx < sideTaskScripts.Count) ? sideTaskScripts[idx] : null;

        main?.OpenAskWindowLocal();
        side?.OpenAskWindowLocal();
    }

    public void OpenSolutionWindowsFor(Task task)
    {
        if (task == null) return;

        int idx = taskScripts.IndexOf(task);
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx < 0) return;

        var main = (idx >= 0 && idx < taskScripts.Count) ? taskScripts[idx] : null;
        var side = (idx >= 0 && idx < sideTaskScripts.Count) ? sideTaskScripts[idx] : null;

        main?.OpenSolutionWindowLocal();
        side?.OpenSolutionWindowLocal();
    }

    int GetCurrentPartIndexForTask(int taskIndex)
    {
        var t = tasks[taskIndex];
        if (!(t.useParts && t.parts != null && t.parts.Count > 0)) return -1;

        for (int p = 0; p < t.parts.Count; p++)
        {
            if (t.parts[p].progress != Verdict.Success) return p;
        }
        return t.parts.Count - 1;
    }

    bool IsTaskCompleted(int taskIndex)
    {
        var t = tasks[taskIndex];
        if (t.useParts && t.parts != null && t.parts.Count > 0)
        {
            for (int p = 0; p < t.parts.Count; p++)
                if (t.parts[p].progress != Verdict.Success) return false;
            return true;
        }
        return t.progress == Verdict.Success;
    }

    int FindFlatIndex(int taskIndex, int partIndex)
    {
        for (int i = 0; i < _flat.Count; i++)
        {
            if (_flat[i].task == taskIndex && _flat[i].part == partIndex) return i;
        }
        return -1;
    }

    int GetFlatIndexForTaskCurrent(int taskIndex)
    {
        var t = tasks[taskIndex];
        if (t.useParts && t.parts != null && t.parts.Count > 0)
        {
            int p = GetCurrentPartIndexForTask(taskIndex);
            return FindFlatIndex(taskIndex, p);
        }
        return FindFlatIndex(taskIndex, -1);
    }

    string GetTaskSelectorLabel(int taskIndex)
    {
        int humanTask = taskIndex + 1;
        var t = tasks[taskIndex];
        if (t.useParts && t.parts != null && t.parts.Count > 0)
        {
            int cur = GetCurrentPartIndexForTask(taskIndex);
            string partLabel = "";
            if (cur >= 0)
            {
                var p = t.parts[cur];
                partLabel = !string.IsNullOrEmpty(p.label) ? p.label : ((char)('a' + cur)).ToString();
            }
            return $"{selectorItemPrefix}{humanTask}{partLabel}";
        }
        return $"{selectorItemPrefix}{humanTask}";
    }

    void UpdateTaskSelectorLabels()
    {
        if (selectorGroups == null) return;
        for (int s = 0; s < selectorGroups.Length; s++)
        {
            var sel = selectorGroups[s].selector;
            if (sel == null || sel.items == null) continue;

            int count = Mathf.Min(sel.items.Count, tasks.Count);
            for (int i = 0; i < count; i++)
            {
                sel.items[i].itemTitle = GetTaskSelectorLabel(i);
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (sel.label != null && sel.items.Count > 0)
                    sel.label.text = sel.items[Mathf.Clamp(sel.index, 0, sel.items.Count - 1)].itemTitle;
                if (sel.labelHelper != null)
                    sel.labelHelper.text = sel.label != null ? sel.label.text :
                        (sel.items.Count > 0 ? sel.items[Mathf.Clamp(sel.index, 0, sel.items.Count - 1)].itemTitle : string.Empty);
            }
            else
            {
                sel.UpdateUI();
            }
#else
            sel.UpdateUI();
#endif
        }
    }

    void UpdateIndicatorsForTask(int taskIndex)
    {
        if (selectorGroups == null) return;
        bool done = IsTaskCompleted(taskIndex);
        for (int i = 0; i < selectorGroups.Length; i++)
        {
            var ind = selectorGroups[i].indicators;
            if (ind == null) continue;
            if (done) ind.SetIndicatorMarked(taskIndex);
            else ind.SetIndicatorUnmarked(taskIndex);
        }
    }

    void GetItemData(int flatIndex,
        out string header, out string body, out string placeholder, out string answerKey,
        out TaskType taskType, out SingleLineSettings sl, out MultiLineSettings ml,
        out CommunicationSettings grading, out AIConditionDescriptions aic)
    {
        var r = _flat[flatIndex];
        var t = tasks[r.task];

        if (IsPart(r))
        {
            var p = t.parts[r.part];
            string partLabel = !string.IsNullOrEmpty(p.label)
                ? p.label
                : ((char)('a' + r.part)).ToString();
            header = string.IsNullOrEmpty(t.header) ? $"({partLabel})" : $"{t.header} ({partLabel})";

            body = p.body;
            placeholder = p.placeholder;
            answerKey = p.answerKey;
            taskType = p.taskType;
            sl = p.singleLineSettings;
            ml = p.multiLineSettings;
        }
        else
        {
            header = t.header;
            body = t.body;
            placeholder = t.placeholder;
            answerKey = t.answerKey;
            taskType = t.taskType;
            sl = t.singleLineSettings;
            ml = t.multiLineSettings;
        }

        grading = t.gradingSettings;
        aic = this.aiConditionDescriptions;
    }

    void RebuildFlatMap()
    {
        _flat.Clear();
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            if (t.useParts && t.parts != null && t.parts.Count > 0)
            {
                for (int p = 0; p < t.parts.Count; p++)
                    _flat.Add(new FlatRef { task = i, part = p });
            }
            else
            {
                _flat.Add(new FlatRef { task = i, part = -1 });
            }
        }
    }

    void EnsureTipListSize()
    {
        int n = Mathf.Max(0, FlatCount);
        if (_tipShown == null) _tipShown = new List<bool>(n);
        if (_tipShown.Count != n)
        {
            _tipShown = new List<bool>(new bool[n]);
        }
    }

    int GetFlatIndexFromTask(Task task)
    {
        int idx = taskScripts.IndexOf(task);
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        return idx;
    }

    bool TipFlagForIndex(int i) => (i >= 0 && i < _tipShown.Count) && _tipShown[i];

    public bool ShouldShowSolutionFor(Task task, Verdict v)
    {
        int idx = GetFlatIndexFromTask(task);
        if (idx < 0) return v == Verdict.Success;
        return v == Verdict.Success || TipFlagForIndex(idx);
    }

    public void HandleTipForTask(Task task)
    {
        // still post the chat “Tip” preset
        SendTip();

        int idx = GetFlatIndexFromTask(task);
        if (idx >= 0)
        {
            EnsureTipListSize();
            _tipShown[idx] = true;
            SaveTipShown();
            OpenSolutionWindowsFor(task);
            ApplyProgressToFlatIndex(idx);
        }
    }

    public void OpenFullscreenView()
    {
        if (fullscreenView != null && !fullscreenView.activeSelf) fullscreenView.SetActive(true);
    }

    public void GoToPrevTask()
    {
        if (FlatCount == 0) return;
        int next = Mathf.Max(0, _currentIndex - 1);
        if (next != _currentIndex) OpenTaskByIndex(next);
    }

    public void GoToNextTask()
    {
        if (FlatCount == 0) return;
        int next = Mathf.Min(FlatCount - 1, _currentIndex + 1);
        if (next != _currentIndex) OpenTaskByIndex(next);
    }

    void OnChanged()
    {
        RebuildFlatMap();
        EnsureTipListSize();

        SetWorkspaceTaskWindows();
        SetSideTaskWindows();
        SyncDualMultiContainerTargets();
        ApplyTaskDataToScripts();
        BuildTaskSelectorItems();
        ApplyProgressToAllIndicators();
        UpdatePrevNextForAllSelectors();
    }

    internal void CreateWorkspaceTaskWindow(int flatIndex, Dictionary<int, Transform> existingByIndex)
    {
        if (workspaceWindowManager == null || taskWindowPrefab == null) return;

        var parent = workspaceWindowManager.transform;
        GameObject go;

        if (existingByIndex != null && existingByIndex.TryGetValue(flatIndex, out var t))
        {
            go = t.gameObject;
            go.transform.SetParent(parent, false);
            go.name = flatIndex.ToString();
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
            go.name = flatIndex.ToString();
        }

        if (taskWindowGOs.Count <= flatIndex) taskWindowGOs.Add(go);
        else taskWindowGOs[flatIndex] = go;

        var taskComp = go.GetComponent<Task>();
        if (taskScripts.Count <= flatIndex) taskScripts.Add(taskComp);
        else taskScripts[flatIndex] = taskComp;

        if (taskComp != null) taskComp.SetTaskManager(this);

        workspaceWindowManager.windows.Add(new WindowManager.WindowItem
        {
            windowName = flatIndex.ToString(),
            windowObject = go
        });
    }

    void SetWorkspaceTaskWindows()
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
                if (idx >= 0 && idx < FlatCount)
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
        taskWindowGOs.Capacity = FlatCount;
        taskScripts.Capacity = FlatCount;

        for (int i = 0; i < FlatCount; i++) CreateWorkspaceTaskWindow(i, existingByIndex);
    }

    internal void CreateSideTaskWindow(int flatIndex, Dictionary<int, Transform> existingByIndex)
    {
        if (sideWindowManager == null || sideTaskWindowPrefab == null) return;

        var parent = sideWindowManager.transform;
        GameObject go;

        if (existingByIndex != null && existingByIndex.TryGetValue(flatIndex, out var t))
        {
            go = t.gameObject;
            go.transform.SetParent(parent, false);
            go.name = flatIndex.ToString();
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
            go.name = flatIndex.ToString();
        }

        if (sideTaskWindowGOs.Count <= flatIndex) sideTaskWindowGOs.Add(go);
        else sideTaskWindowGOs[flatIndex] = go;

        var taskComp = go.GetComponent<SideTask>();
        if (sideTaskScripts.Count <= flatIndex) sideTaskScripts.Add(taskComp);
        else sideTaskScripts[flatIndex] = taskComp;

        if (taskComp != null) taskComp.SetTaskManager(this);

        sideWindowManager.windows.Add(new WindowManager.WindowItem
        {
            windowName = flatIndex.ToString(),
            windowObject = go
        });
    }

    void SetSideTaskWindows()
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
                if (idx >= 0 && idx < FlatCount)
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
        sideTaskWindowGOs.Clear();
        sideTaskWindowGOs.Capacity = FlatCount;
        sideTaskScripts.Capacity = FlatCount;

        for (int i = 0; i < FlatCount; i++) CreateSideTaskWindow(i, existingByIndex);
    }

    // Return the effective base parent padding for a given flat item (before adding workspace extra).
    float GetEffectiveParentPaddingBase(int flatIndex)
    {
        float globalPad = Mathf.Max(0f, parentPaddingSideGlobal);

        if (flatIndex < 0 || flatIndex >= FlatCount) return globalPad;

        var r = _flat[flatIndex];
        var t = tasks[r.task];

        if (IsPart(r) && t.parts != null && r.part >= 0 && r.part < t.parts.Count)
        {
            var p = t.parts[r.part];
            if (p.doOverrideParentPadding)
                return Mathf.Max(0f, p.singleLineSettings.parentPaddingSide);
        }
        else
        {
            // Single-part task (no parts)
            if (!t.useParts && t.doOverrideParentPadding)
                return Mathf.Max(0f, t.singleLineSettings.parentPaddingSide);
        }

        // Fallback to global
        return globalPad;
    }

    void ApplyTaskDataToScripts()
    {
        int count = FlatCount;

        // MAIN
        for (int i = 0; i < count; i++)
        {
            if (i >= taskScripts.Count) break;
            var view = taskScripts[i];
            if (view == null) continue;

            GetItemData(i,
                out string header, out string body, out string placeholder, out string answerKey,
                out TaskType type, out SingleLineSettings sl, out MultiLineSettings ml,
                out CommunicationSettings grading, out AIConditionDescriptions aic);

            view.SetData(header, body, answerKey);
            view.SetPlaceholder(sl.inputFitMode == InputFitMode.Absolute ? "" : sl.leftText);
            view.SetWindowByTaskType(type == TaskType.MultiLine);
            view.SetNextToggleByHasRight(i < count - 1);

            var grow = view.SingleLineAutoGrow != null
                ? view.SingleLineAutoGrow
                : view.GetComponentInChildren<AutoGrowToText>(true);

            if (grow != null)
            {
                grow.SetLeftText(sl.leftText);
                grow.SetRightText(sl.rightText);
                grow.SetPlaceholder(GetPlaceholderFor(i));
                grow.SetFitMode(sl.inputFitMode);
                if (sl.inputFitMode == InputFitMode.Absolute)
                {
                    float min = Mathf.Max(1f, sl.minWidth);
                    float max = Mathf.Max(min, sl.maxWidth);
                    grow.SetMinMaxWidth(min, max);
                }
                else
                {
                    // Use global or override, then add workspace extra
                    float basePad = GetEffectiveParentPaddingBase(i);
                    float pad = Mathf.Max(0f, basePad + workspacePaddingExtra);
                    grow.SetParentPadding(pad);
                }
                grow.ForceRecomputeNow();
            }

            if (i < taskWindowGOs.Count)
            {
                var go = taskWindowGOs[i];
                if (go != null)
                {
                    var fields = go.GetComponentsInChildren<UserAnswerField>(true);
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var f = fields[j];
                        f.answerKey = answerKey;
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
                        f.SetPlaceholder(GetPlaceholderFor(i));

                        if (f is UserMultiLineAnswerField)
                        {
                            f.SetCheckMode(ml.checkMode);
                            f.SetAllowAIThinking(ml.allowAIThinking);
                        }
                        else
                        {
                            f.SetCaseSensitive(sl.caseSensitive);
                            f.SetCheckMode(sl.checkMode);
                        }

                        f.SetGradingSettings(grading);
                        f.SetAIInstructions(aic.isCorrect, aic.isAlmost, aic.isAlmostFeedback, aic.doGiveAlmostFeedback);

                        f.ApplyProgressState(GetItemProgress(i));
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

            GetItemData(i,
                out string header, out string body, out string placeholder, out string answerKey,
                out TaskType type, out SingleLineSettings sl, out MultiLineSettings ml,
                out CommunicationSettings grading, out AIConditionDescriptions aic);

            view.SetData(header, body, answerKey);
            view.SetPlaceholder(GetPlaceholderFor(i));
            view.SetWindowByTaskType(type == TaskType.MultiLine);
            view.SetNextToggleByHasRight(i < count - 1);

            var grow = view.SingleLineAutoGrow != null
                ? view.SingleLineAutoGrow
                : view.GetComponentInChildren<AutoGrowToText>(true);

            if (grow != null)
            {
                grow.SetLeftText(sl.leftText);
                grow.SetRightText(sl.rightText);
                grow.SetPlaceholder(GetPlaceholderFor(i));
                grow.SetFitMode(sl.inputFitMode);
                if (sl.inputFitMode == InputFitMode.Absolute)
                {
                    float min = Mathf.Max(1f, sl.minWidth);
                    float max = Mathf.Max(min, sl.maxWidth);
                    grow.SetMinMaxWidth(min, max);
                }
                else
                {
                    // Use global or override (no workspace extra on side)
                    float pad = Mathf.Max(0f, GetEffectiveParentPaddingBase(i));
                    grow.SetParentPadding(pad);
                }
                grow.ForceRecomputeNow();
            }

            if (i < sideTaskWindowGOs.Count)
            {
                var go = sideTaskWindowGOs[i];
                if (go != null)
                {
                    var fields = go.GetComponentsInChildren<UserAnswerField>(true);
                    for (int j = 0; j < fields.Length; j++)
                    {
                        var f = fields[j];
                        f.answerKey = answerKey;
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
                        f.SetPlaceholder(GetPlaceholderFor(i));

                        if (f is UserMultiLineAnswerField)
                        {
                            f.SetCheckMode(ml.checkMode);
                            f.SetAllowAIThinking(ml.allowAIThinking);
                        }
                        else
                        {
                            f.SetCaseSensitive(sl.caseSensitive);
                            f.SetCheckMode(sl.checkMode);
                        }

                        f.SetGradingSettings(grading);
                        f.SetAIInstructions(aic.isCorrect, aic.isAlmost, aic.isAlmostFeedback, aic.doGiveAlmostFeedback);

                        f.ApplyProgressState(GetItemProgress(i));
                    }
                }
            }
        }
    }

    string GetPlaceholderFor(int flatIndex)
    {
        var r = _flat[flatIndex];
        var t = tasks[r.task];
        if (IsPart(r))
        {
            var p = t.parts[r.part];
            return string.IsNullOrEmpty(p.placeholder) ? t.placeholder : p.placeholder;
        }
        return t.placeholder;
    }

    void SyncDualMultiContainerTargets()
    {
        if (dualMultiContainer == null) return;

        int n = FlatCount;
        var defaults = new RectTransform[n];
        var alts = new RectTransform[n];

        for (int i = 0; i < n; i++)
        {
            SideTask st = (i < sideTaskScripts.Count) ? sideTaskScripts[i] : null;
            if (st != null)
            {
                defaults[i] = st.multiLineStretchTarget;
                alts[i] = st.singleLineStretchTarget;
            }
        }

        dualMultiContainer.stretchTargets = defaults;
        dualMultiContainer.altStretchTargets = alts;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            dualMultiContainer.SetStretchTargetAlt(false);
        }
        else
        {
            dualMultiContainer.InitDisplay();
        }
#else
        dualMultiContainer.InitDisplay();
#endif
    }

    void SetActiveStretchTargets(int activeIndex)
    {
        if (dualMultiContainer == null) return;

        int n = Mathf.Max(1, FlatCount);
        if (_activeDefaults == null || _activeDefaults.Length != n) _activeDefaults = new RectTransform[n];
        if (_activeAlts == null || _activeAlts.Length != n) _activeAlts = new RectTransform[n];

        for (int i = 0; i < n; i++) { _activeDefaults[i] = null; _activeAlts[i] = null; }

        if (activeIndex >= 0 && activeIndex < sideTaskScripts.Count)
        {
            var st = sideTaskScripts[activeIndex];
            if (st != null)
            {
                _activeDefaults[activeIndex] = st.multiLineStretchTarget;
                _activeAlts[activeIndex] = st.singleLineStretchTarget;
            }
        }

        dualMultiContainer.stretchTargets = _activeDefaults;
        dualMultiContainer.altStretchTargets = _activeAlts;
    }

    void UpdateDualMultiContainerForIndex(int index)
    {
        if (!Application.isPlaying || dualMultiContainer == null) return;

        SetActiveStretchTargets(index);
        bool useAltForSingleLine = (index >= 0 && index < FlatCount) &&
                                   GetItemIsSingleLine(index);
        dualMultiContainer.SetStretchTargetAlt(useAltForSingleLine);
    }

    bool GetItemIsSingleLine(int flatIndex)
    {
        GetItemData(flatIndex,
            out _, out _, out _, out _,
            out TaskType type, out _, out _,
            out _, out _);
        return type == TaskType.SingleLine;
    }

    void BuildTaskSelectorItems()
    {
        if (selectorGroups == null || selectorGroups.Length == 0) return;

        int countTasks = Mathf.Max(0, tasks.Count);
        int currentTaskIndex = (_currentIndex >= 0 && _currentIndex < FlatCount) ? _flat[_currentIndex].task : 0;
        int prevIndex = Mathf.Clamp(currentTaskIndex, 0, Mathf.Max(0, countTasks - 1));

        for (int s = 0; s < selectorGroups.Length; s++)
        {
            var sel = selectorGroups[s].selector;
            if (sel == null) continue;

            sel.saveSelected = false;

            var newItems = new List<HorizontalSelector.Item>(countTasks);
            for (int i = 0; i < countTasks; i++)
            {
                int capturedTaskIndex = i;
                var item = new HorizontalSelector.Item { itemTitle = GetTaskSelectorLabel(i) };
                item.onItemSelect.AddListener(() =>
                {
                    int flat = GetFlatIndexForTaskCurrent(capturedTaskIndex);
                    if (flat >= 0) OpenTaskByIndex(flat);
                });
                newItems.Add(item);
            }

            sel.items = newItems;
            sel.defaultIndex = prevIndex;
            sel.index = prevIndex;

            sel.onValueChanged.RemoveAllListeners();
            sel.onValueChanged.AddListener((selectedTaskIndex) =>
            {
                int flat = GetFlatIndexForTaskCurrent(selectedTaskIndex);
                if (flat >= 0) OpenTaskByIndex(flat);
            });

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (sel.label != null && sel.items.Count > 0)
                    sel.label.text = sel.items[Mathf.Clamp(sel.index, 0, sel.items.Count - 1)].itemTitle;
                if (sel.labelHelper != null)
                    sel.labelHelper.text = sel.label != null ? sel.label.text :
                        (sel.items.Count > 0 ? sel.items[Mathf.Clamp(sel.index, 0, sel.items.Count - 1)].itemTitle : string.Empty);
                ClearSelectorIndicatorsImmediate(sel);
            }
            else
            {
                sel.UpdateUI();
            }
#else
            sel.UpdateUI();
#endif
            ApplyProgressToIndicators(selectorGroups[s]);
        }

        UpdatePrevNextForAllSelectors();
    }

    void ClearSelectorIndicatorsImmediate(HorizontalSelector sel)
    {
        if (sel == null || sel.indicatorParent == null) return;
        var toKill = new List<GameObject>();
        foreach (Transform child in sel.indicatorParent) toKill.Add(child.gameObject);
#if UNITY_EDITOR
        if (!Application.isPlaying) toKill.ForEach(DestroyImmediate);
        else toKill.ForEach(Destroy);
#else
        toKill.ForEach(Destroy);
#endif
    }

    void ApplyProgressToIndicators(SelectorGroup group)
    {
        if (group.indicators == null) return;

        for (int i = 0; i < tasks.Count; i++)
        {
            if (IsTaskCompleted(i)) group.indicators.SetIndicatorMarked(i);
            else group.indicators.SetIndicatorUnmarked(i);
        }
    }

    void ApplyProgressToAllIndicators()
    {
        if (selectorGroups == null) return;
        for (int i = 0; i < selectorGroups.Length; i++) ApplyProgressToIndicators(selectorGroups[i]);
    }

    void UpdatePrevNextForAllSelectors()
    {
        if (selectorGroups == null) return;

        int currentTaskIndex = (_currentIndex >= 0 && _currentIndex < FlatCount) ? _flat[_currentIndex].task : 0;

        for (int i = 0; i < selectorGroups.Length; i++)
        {
            var grp = selectorGroups[i];
            if (grp.indicators == null || grp.selector == null) continue;
            grp.indicators.SetPrevNext(currentTaskIndex, tasks.Count);
        }
    }

    public void OnAnswerFieldCorrect(Task task)
    {
        if (task == null) return;

        int idx = taskScripts.IndexOf(task);
        bool isWorkspaceTask = idx >= 0;
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx >= 0)
        {
            // mark solved
            SetItemProgress(idx, Verdict.Success);
            // clear any tip flag since it's solved now
            EnsureTipListSize();
            if (idx < _tipShown.Count) _tipShown[idx] = false;
            SaveTipShown();

            UpdateTaskSelectorLabels();
            int parentTask = _flat[Mathf.Clamp(idx, 0, _flat.Count - 1)].task;
            UpdateIndicatorsForTask(parentTask);
        }

        if (correctIconFeedback == CorrectIconFeedback.Never) return;

        bool canPlay =
            correctIconFeedback == CorrectIconFeedback.Always ||
            (correctIconFeedback == CorrectIconFeedback.OnlyInWorkspaceView && isWorkspaceTask && task.gameObject.activeInHierarchy);

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
        if (idx < 0 || idx >= FlatCount - 1) return;
        OpenTaskByIndex(idx + 1);
    }

    void ActivateConfigForTask(int flatIndex)
    {
        if (configHelper == null) return;
        if (flatIndex < 0 || flatIndex >= FlatCount) return;

        string configName = tasks[_flat[flatIndex].task].header;
        if (string.IsNullOrEmpty(configName)) return;

        configHelper.SetActiveConfigByName(targetCollectionName, configName);
    }

    public void OpenTaskByIndex(int index)
    {
        if (index < 0 || index >= FlatCount) return;
        _currentIndex = index;

        UpdateDualMultiContainerForIndex(index);

        if (workspaceWindowManager != null) workspaceWindowManager.OpenWindowByIndex(index);
        if (sideWindowManager != null) sideWindowManager.OpenWindowByIndex(index);

        int currentTaskIndex = _flat[index].task;

        if (selectorGroups != null)
        {
            for (int s = 0; s < selectorGroups.Length; s++)
            {
                var sel = selectorGroups[s].selector;
                if (sel == null) continue;

                sel.index = currentTaskIndex;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    if (sel.label != null && sel.items.Count > 0)
                        sel.label.text = sel.items[Mathf.Clamp(sel.index, 0, sel.items.Count - 1)].itemTitle;

                    if (sel.labelHelper != null)
                        sel.labelHelper.text = sel.label != null
                            ? sel.label.text
                            : (sel.items.Count > 0 ? sel.items[Mathf.Clamp(sel.index, 0, sel.items.Count - 1)].itemTitle : string.Empty);
                }
                else
                {
                    sel.UpdateUI();
                }
#else
                sel.UpdateUI();
#endif
            }
        }

        UpdatePrevNextForAllSelectors();
        ApplyProgressToAllIndicators();
        UpdateTaskSelectorLabels();
        ActivateConfigForTask(index);
        if (pngViewer != null) pngViewer.Disable();

        if (Application.isPlaying) SaveCurrentIndex();
    }

    public void SendTip()
    {
        if (assistantChatManager != null) assistantChatManager.SendPresetUserMessage("Tip");
    }

    void Awake() => UpdateNeighbors();

    void OnEnable()
    {
        RebuildFlatMap();
        EnsureTipListSize();

        if (Application.isPlaying) LoadProgressIfAvailable();
        if (Application.isPlaying) LoadTipShownIfAvailable();
        UpdateNeighbors();

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= HandleEditorPlayModeChanged;
        EditorApplication.playModeStateChanged += HandleEditorPlayModeChanged;
#endif

        if (Application.isPlaying && !_didRuntimeInit)
        {
            _didRuntimeInit = true;

            SetWorkspaceTaskWindows();
            SetSideTaskWindows();
            SyncDualMultiContainerTargets();
            ApplyTaskDataToScripts();

            int startIndex = Mathf.Clamp(LoadSavedCurrentIndex(), 0, Mathf.Max(0, FlatCount - 1));
            _currentIndex = startIndex;

            BuildTaskSelectorItems();

            UpdateDualMultiContainerForIndex(_currentIndex);
            OpenTaskByIndex(_currentIndex);

            ApplyProgressToAllIndicators();
            ApplyProgressToAllAnswerFields();

            if (_consistencyLoop == null) _consistencyLoop = StartCoroutine(ConsistencyHeartbeat());
        }
        else
        {
            ApplyProgressToAllIndicators();
            UpdatePrevNextForAllSelectors();
            UpdateTaskSelectorLabels();
        }

        if (Application.isPlaying && _consistencyLoop == null)
            _consistencyLoop = StartCoroutine(ConsistencyHeartbeat());
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= HandleEditorPlayModeChanged;
#endif
        if (_consistencyLoop != null)
        {
            StopCoroutine(_consistencyLoop);
            _consistencyLoop = null;
        }
    }

    void OnDestroy()
    { 
        if (Application.isPlaying)
        {
            SaveProgress();
            SaveTipShown();
            SaveCurrentIndex();
        }
    }

    IEnumerator ConsistencyHeartbeat()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            if (selectorGroups != null)
            {
                int curTaskIndex = (_currentIndex >= 0 && _currentIndex < FlatCount) ? _flat[_currentIndex].task : 0;
                for (int i = 0; i < selectorGroups.Length; i++)
                {
                    var sel = selectorGroups[i].selector;
                    if (sel == null) continue;
                    if (sel.index != curTaskIndex)
                    {
                        sel.index = curTaskIndex;
                        sel.UpdateUI();
                    }
                }
            }

            ApplyProgressToAllIndicators();
            ApplyProgressToAllAnswerFields();
            UpdatePrevNextForAllSelectors();
            UpdateTaskSelectorLabels();

            if (Application.isPlaying) SaveCurrentIndex();

            SaveProgress();
            SaveTipShown();
            yield return wait;
        }
    }

    void UpdateNeighbors()
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            t.SetNeighbors(i > 0, i < tasks.Count - 1);
            tasks[i] = t;
        }
    }

    public void SetItemProgress(int flatIndex, Verdict progress)
    {
        if (flatIndex < 0 || flatIndex >= FlatCount) return;

        var r = _flat[flatIndex];

        SetItemProgressInternal(flatIndex, progress);
        UpdateIndicatorsForTask(r.task);

        // If user solved it, clear any previous tip flag for this item
        if (progress == Verdict.Success)
        {
            EnsureTipListSize();
            if (flatIndex < _tipShown.Count) _tipShown[flatIndex] = false;
            SaveTipShown();
        }

        ApplyProgressToFlatIndex(flatIndex);
        if (Application.isPlaying) SaveProgress();
    }

    public void SetAllTaskProgress(Verdict progress)
    {
        for (int i = 0; i < FlatCount; i++) SetItemProgressInternal(i, progress);

        ApplyProgressToAllIndicators();
        ApplyProgressToAllAnswerFields();
        UpdateTaskSelectorLabels();
        if (Application.isPlaying) SaveProgress();
    }

    public void SetTaskProgressByHeader(string header, Verdict progress)
    {
        if (string.IsNullOrEmpty(header)) return;
        for (int i = 0; i < FlatCount; i++)
        {
            var r = _flat[i];
            if (tasks[r.task].header == header) SetItemProgressInternal(i, progress);
        }

        ApplyProgressToAllIndicators();
        ApplyProgressToAllAnswerFields();
        UpdateTaskSelectorLabels();
        if (Application.isPlaying) SaveProgress();
    }

    void ApplyProgressToFlatIndex(int i)
    {
        if (i < 0 || i >= FlatCount) return;
        var v = GetItemProgress(i);

        if (i < taskWindowGOs.Count && taskWindowGOs[i] != null)
        {
            var fields = taskWindowGOs[i].GetComponentsInChildren<UserAnswerField>(true);
            for (int f = 0; f < fields.Length; f++) fields[f].ApplyProgressState(v);
        }
        if (i < sideTaskWindowGOs.Count && sideTaskWindowGOs[i] != null)
        {
            var fields = sideTaskWindowGOs[i].GetComponentsInChildren<UserAnswerField>(true);
            for (int f = 0; f < fields.Length; f++) fields[f].ApplyProgressState(v);
        }

        var taskView = (i < taskScripts.Count) ? taskScripts[i] : null;
        if (taskView != null) taskView.ApplyProgressToNextToggles(v);

        var sideView = (i < sideTaskScripts.Count) ? sideTaskScripts[i] : null;
        if (sideView != null) sideView.ApplyProgressToNextToggles(v);
    }

    void ApplyProgressToAllAnswerFields()
    {
        for (int i = 0; i < FlatCount; i++) ApplyProgressToFlatIndex(i);
    }

    void SaveProgress()
    {
        if (progressStorage == null) return;
        var arr = new int[FlatCount];
        for (int i = 0; i < FlatCount; i++) arr[i] = (int)GetItemProgress(i);
        progressStorage.SetValue(arr);
    }

    void LoadProgressIfAvailable()
    {
        if (progressStorage == null || !DataStorage.hasValue) return;
        var loaded = progressStorage.GetValue<int[]>();
        if (loaded == null || loaded.Length == 0) return;

        int n = Mathf.Min(FlatCount, loaded.Length);
        for (int i = 0; i < n; i++)
        {
            var v = (Verdict)Mathf.Clamp(loaded[i], 0, (int)Verdict.Almost);
            SetItemProgressInternal(i, v);
        }
    }

    void ClearPerPlayStorages()
    {
        RebuildFlatMap();

        if (progressStorage != null)
        {
            var arr = new int[FlatCount];
            progressStorage.SetValue(arr);
        }

        if (tipShownStorage != null)
        {
            var arr = new int[FlatCount];
            tipShownStorage.SetValue(arr);
        }

        if (selectedIndexStorage != null)
        {
            selectedIndexStorage.SetValue(0);
        }
    }

    void SaveTipShown()
    {
        if (tipShownStorage == null) return;
        EnsureTipListSize();
        var arr = new int[_tipShown.Count];
        for (int i = 0; i < _tipShown.Count; i++) arr[i] = _tipShown[i] ? 1 : 0;
        tipShownStorage.SetValue(arr);
    }

    void LoadTipShownIfAvailable()
    {
        if (tipShownStorage == null) return;
        var loaded = tipShownStorage.GetValue<int[]>();
        if (loaded == null || loaded.Length == 0) return;

        EnsureTipListSize();
        int n = Mathf.Min(_tipShown.Count, loaded.Length);
        for (int i = 0; i < n; i++)
        {
            _tipShown[i] = loaded[i] != 0;
        }
    }

    void SaveCurrentIndex()
    {
        if (!Application.isPlaying || selectedIndexStorage == null) return;
        selectedIndexStorage.SetValue(_currentIndex);
    }

    int LoadSavedCurrentIndex()
    {
        if (!Application.isPlaying || selectedIndexStorage == null) return 0;
        int saved = selectedIndexStorage.GetValue<int>();
        return saved;
    }

    [System.Serializable]
    struct AnswerFieldColors
    {
        [ColorUsage(true, true)] public Color normal, edit, success, fail, almost;
    }

    [System.Serializable]
    struct FeedbackAnimations
    {
        public float shakePixels, shakeCycleDuration, shakeCycles, outlineLerp;
    }

    enum CorrectIconFeedback { Always, OnlyInWorkspaceView, Never }
    public enum TaskType { SingleLine, MultiLine }

    [System.Serializable]
    struct AIConditionDescriptions
    {
        [TextArea(2, 40)] public string isCorrect, isAlmost, isAlmostFeedback;
        public bool doGiveAlmostFeedback;
    }

    [System.Serializable]
    public struct SingleLineSettings
    {
        public bool caseSensitive;
        public CheckMode checkMode;
        public string leftText, rightText;
        public InputFitMode inputFitMode;
        public float minWidth;
        public float maxWidth;

        public float parentPaddingSide;
    }

    [System.Serializable]
    public struct MultiLineSettings
    {
        public CheckMode checkMode;
        public bool allowAIThinking;
    }

    [System.Serializable]
    public struct UserTaskPart
    {
        [Tooltip("Part label used in selector title, e.g., a, b, c. If empty, auto a/b/c...")]
        public string label;

        [TextArea(6, 40)] public string body;
        public string placeholder;
        [TextArea(2, 40)] public string answerKey;

        public TaskType taskType;
        public SingleLineSettings singleLineSettings;
        public MultiLineSettings multiLineSettings;

        [Tooltip("If true, this part will use its own SingleLineSettings.parentPaddingSide instead of the global value in TaskManager (when fit is relative).")]
        public bool doOverrideParentPadding;

        [HideInInspector] public Verdict progress;
    }

    [System.Serializable]
    struct UserTask
    {
        public string header;

        [TextArea(6, 40)] public string body;
        public string placeholder;
        [TextArea(2, 40)] public string answerKey;

        public CommunicationSettings gradingSettings;

        public TaskType taskType;
        public SingleLineSettings singleLineSettings;
        public MultiLineSettings multiLineSettings;

        public bool useParts;
        public List<UserTaskPart> parts;

        [Tooltip("If true (and not using parts), this task will use its own SingleLineSettings.parentPaddingSide instead of the global value (when fit is relative).")]
        public bool doOverrideParentPadding;

        public Sprite[] solutionSprites;

        [HideInInspector] public bool hasLeftTask, hasRightTask;
        [HideInInspector] public Verdict progress;

        public void SetNeighbors(bool hasLeft, bool hasRight)
        {
            hasLeftTask = hasLeft; hasRightTask = hasRight;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(UserTask))]
    class UserTaskDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return h;

            float v = EditorGUIUtility.standardVerticalSpacing;

            float GetH(string rel, bool includeChildren = true)
            {
                var p = property.FindPropertyRelative(rel);
                return p == null ? 0f : EditorGUI.GetPropertyHeight(p, includeChildren);
            }

            // header
            h += GetH("header") + v;

            // useParts toggle
            h += EditorGUIUtility.singleLineHeight + v;

            var useParts = property.FindPropertyRelative("useParts");
            if (useParts.boolValue)
            {
                h += GetH("gradingSettings") + v;
                h += GetH("solutionSprites") + v;
                h += GetH("parts") + v;
            }
            else
            {
                h += GetH("body") + v;
                h += GetH("placeholder") + v;
                h += GetH("answerKey") + v;
                h += GetH("gradingSettings") + v;
                h += GetH("solutionSprites") + v;
                h += GetH("taskType") + v;

                // override toggle (single line tasks feature; show line height always to keep UI stable)
                h += EditorGUIUtility.singleLineHeight + v;

                // settings block depends on task type
                var tt = property.FindPropertyRelative("taskType");
                if ((TaskType)tt.enumValueIndex == TaskType.SingleLine)
                {
                    var sl = property.FindPropertyRelative("singleLineSettings");

                    h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("caseSensitive"), true) + v;
                    h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("checkMode"), true) + v;
                    h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("leftText"), true) + v;
                    h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("rightText"), true) + v;

                    // inputFitMode
                    var fit = sl.FindPropertyRelative("inputFitMode");
                    h += EditorGUIUtility.singleLineHeight + v;

                    if ((InputFitMode)fit.enumValueIndex == InputFitMode.Absolute)
                    {
                        h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("minWidth"), true) + v;
                        h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("maxWidth"), true) + v;
                    }
                    else
                    {
                        var doOverride = property.FindPropertyRelative("doOverrideParentPadding");
                        if (doOverride.boolValue)
                        {
                            h += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("parentPaddingSide"), true) + v;
                        }
                    }
                }
                else
                {
                    h += GetH("multiLineSettings") + v;
                }
            }

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

            Rect Row(float height) { var rr = new Rect(position.x, y, position.width, height); y += height + v; return rr; }

            void DrawRel(string rel, bool includeChildren = true)
            {
                var p = property.FindPropertyRelative(rel);
                if (p == null) return;
                float h = EditorGUI.GetPropertyHeight(p, includeChildren);
                EditorGUI.PropertyField(Row(h), p, includeChildren);
            }

            DrawRel("header");

            // useParts toggle
            var useParts = property.FindPropertyRelative("useParts");
            useParts.boolValue = EditorGUI.ToggleLeft(Row(line), "Use Parts (1a, 1b, 1c ...)", useParts.boolValue);

            if (useParts.boolValue)
            {
                DrawRel("gradingSettings");
                DrawRel("solutionSprites");
                DrawRel("parts");
            }
            else
            {
                DrawRel("body");
                DrawRel("placeholder");
                DrawRel("answerKey");
                DrawRel("gradingSettings");
                DrawRel("solutionSprites");
                DrawRel("taskType");

                // Override toggle (task-level)
                var doOverride = property.FindPropertyRelative("doOverrideParentPadding");
                doOverride.boolValue = EditorGUI.ToggleLeft(Row(line), "Override Parent Padding (single-line, relative fit)", doOverride.boolValue);

                var tt = property.FindPropertyRelative("taskType");
                if ((TaskType)tt.enumValueIndex == TaskType.SingleLine)
                {
                    var sl = property.FindPropertyRelative("singleLineSettings");

                    EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("caseSensitive"), true)),
                        sl.FindPropertyRelative("caseSensitive"));
                    EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("checkMode"), true)),
                        sl.FindPropertyRelative("checkMode"));
                    EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("leftText"), true)),
                        sl.FindPropertyRelative("leftText"));
                    EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("rightText"), true)),
                        sl.FindPropertyRelative("rightText"));

                    var fitProp = sl.FindPropertyRelative("inputFitMode");
                    EditorGUI.PropertyField(Row(EditorGUIUtility.singleLineHeight), fitProp);

                    if ((InputFitMode)fitProp.enumValueIndex == InputFitMode.Absolute)
                    {
                        EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("minWidth"), true)),
                            sl.FindPropertyRelative("minWidth"));
                        EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("maxWidth"), true)),
                            sl.FindPropertyRelative("maxWidth"));
                    }
                    else
                    {
                        if (doOverride.boolValue)
                        {
                            EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("parentPaddingSide"), true)),
                                sl.FindPropertyRelative("parentPaddingSide"));
                        }
                    }
                }
                else
                {
                    DrawRel("multiLineSettings");
                }
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }

    [CustomPropertyDrawer(typeof(SingleLineSettings))]
    class SingleLineSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            // This drawer is used when SingleLineSettings is shown standalone (e.g., arrays).
            // The task/part drawers handle the conditional visibility of parentPaddingSide themselves.
            float h = 0f, v = EditorGUIUtility.standardVerticalSpacing;
            float line(string rel) => EditorGUI.GetPropertyHeight(property.FindPropertyRelative(rel), true);

            h += line("caseSensitive") + v;
            h += line("checkMode") + v;
            h += line("leftText") + v;
            h += line("rightText") + v;

            var fit = property.FindPropertyRelative("inputFitMode");
            h += EditorGUIUtility.singleLineHeight + v;
            if ((InputFitMode)fit.enumValueIndex == InputFitMode.Absolute)
            {
                h += line("minWidth") + v;
                h += line("maxWidth") + v;
            }
            else
            {
                h += line("parentPaddingSide") + v;
            }
            return h;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            float y = position.y, v = EditorGUIUtility.standardVerticalSpacing, w = position.width;
            Rect Row(float height) { var r = new Rect(position.x, y, w, height); y += height + v; return r; }

            EditorGUI.BeginProperty(position, label, property);
            EditorGUI.indentLevel++;

            EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("caseSensitive"), true)),
                property.FindPropertyRelative("caseSensitive"));
            EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("checkMode"), true)),
                property.FindPropertyRelative("checkMode"));
            EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("leftText"), true)),
                property.FindPropertyRelative("leftText"));
            EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("rightText"), true)),
                property.FindPropertyRelative("rightText"));

            var fitProp = property.FindPropertyRelative("inputFitMode");
            EditorGUI.PropertyField(Row(EditorGUIUtility.singleLineHeight), fitProp);

            if ((InputFitMode)fitProp.enumValueIndex == InputFitMode.Absolute)
            {
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("minWidth"), true)),
                    property.FindPropertyRelative("minWidth"));
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("maxWidth"), true)),
                    property.FindPropertyRelative("maxWidth"));
            }
            else
            {
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("parentPaddingSide"), true)),
                    property.FindPropertyRelative("parentPaddingSide"));
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }

    // Custom drawer for UserTaskPart to hide parentPaddingSide unless doOverrideParentPadding == true
    [CustomPropertyDrawer(typeof(UserTaskPart))]
    class UserTaskPartDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float h = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded) return h;

            float v = EditorGUIUtility.standardVerticalSpacing;

            float Add(string rel, bool includeChildren = true)
            {
                var p = property.FindPropertyRelative(rel);
                if (p == null) return 0f;
                return EditorGUI.GetPropertyHeight(p, includeChildren) + v;
            }

            float singleLineBlockHeight()
            {
                var sl = property.FindPropertyRelative("singleLineSettings");
                float total = 0f;

                total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("caseSensitive"), true) + v;
                total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("checkMode"), true) + v;
                total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("leftText"), true) + v;
                total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("rightText"), true) + v;

                var fit = sl.FindPropertyRelative("inputFitMode");
                total += EditorGUIUtility.singleLineHeight + v;

                if ((InputFitMode)fit.enumValueIndex == InputFitMode.Absolute)
                {
                    total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("minWidth"), true) + v;
                    total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("maxWidth"), true) + v;
                }
                else
                {
                    var doOverride = property.FindPropertyRelative("doOverrideParentPadding");
                    if (doOverride.boolValue)
                    {
                        total += EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("parentPaddingSide"), true) + v;
                    }
                }

                return total;
            }

            h += Add("label");
            h += Add("body");
            h += Add("placeholder");
            h += Add("answerKey");
            h += Add("taskType");

            // override toggle
            h += EditorGUIUtility.singleLineHeight + v;

            var tt = property.FindPropertyRelative("taskType");
            if ((TaskType)tt.enumValueIndex == TaskType.SingleLine)
                h += singleLineBlockHeight();
            else
                h += Add("multiLineSettings");

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

            Rect Row(float height) { var rr = new Rect(position.x, y, position.width, height); y += height + v; return rr; }

            void DrawProp(string rel, bool includeChildren = true)
            {
                var p = property.FindPropertyRelative(rel);
                if (p == null) return;
                float h = EditorGUI.GetPropertyHeight(p, includeChildren);
                EditorGUI.PropertyField(Row(h), p, includeChildren);
            }

            DrawProp("label");
            DrawProp("body");
            DrawProp("placeholder");
            DrawProp("answerKey");
            DrawProp("taskType");

            var doOverride = property.FindPropertyRelative("doOverrideParentPadding");
            doOverride.boolValue = EditorGUI.ToggleLeft(Row(line), "Override Parent Padding (single-line, relative fit)", doOverride.boolValue);

            var tt = property.FindPropertyRelative("taskType");
            if ((TaskType)tt.enumValueIndex == TaskType.SingleLine)
            {
                var sl = property.FindPropertyRelative("singleLineSettings");

                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("caseSensitive"), true)),
                    sl.FindPropertyRelative("caseSensitive"));
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("checkMode"), true)),
                    sl.FindPropertyRelative("checkMode"));
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("leftText"), true)),
                    sl.FindPropertyRelative("leftText"));
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("rightText"), true)),
                    sl.FindPropertyRelative("rightText"));

                var fitProp = sl.FindPropertyRelative("inputFitMode");
                EditorGUI.PropertyField(Row(EditorGUIUtility.singleLineHeight), fitProp);

                if ((InputFitMode)fitProp.enumValueIndex == InputFitMode.Absolute)
                {
                    EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("minWidth"), true)),
                        sl.FindPropertyRelative("minWidth"));
                    EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("maxWidth"), true)),
                        sl.FindPropertyRelative("maxWidth"));
                }
                else
                {
                    if (doOverride.boolValue)
                    {
                        EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(sl.FindPropertyRelative("parentPaddingSide"), true)),
                            sl.FindPropertyRelative("parentPaddingSide"));
                    }
                }
            }
            else
            {
                DrawProp("multiLineSettings");
            }

            EditorGUI.indentLevel--;
            EditorGUI.EndProperty();
        }
    }

    int _lastHash;
    double _nextCheckTime;

    void Update() => EditorHashTick();

    void EditorHashTick()
    {
#if UNITY_EDITOR
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextCheckTime) return;
        _nextCheckTime = now + 0.1f;

        int h = ComputeStateHash();
        if (h != _lastHash)
        {
            _lastHash = h;
            OnChanged();
        }
#endif
    }

    void HandleEditorPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) ResetSelectorsToFirstInEditor();
        if (change == PlayModeStateChange.ExitingEditMode) ClearPerPlayStorages();
    }

    void ResetSelectorsToFirstInEditor()
    {
        if (FlatCount == 0) return;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            ForceSelectorsToZero();
            OpenTaskByIndex(0);
            ApplyProgressToAllIndicators();
            UpdateTaskSelectorLabels();
        };
    }
#endif
    int ComputeStateHash()
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

            hash = hash * 23 + workspacePaddingExtra.GetHashCode();
            hash = hash * 23 + parentPaddingSideGlobal.GetHashCode();

            // Global AI condition descriptions
            hash = hash * 23 + (aiConditionDescriptions.isCorrect != null ? aiConditionDescriptions.isCorrect.GetHashCode() : 0);
            hash = hash * 23 + (aiConditionDescriptions.isAlmost != null ? aiConditionDescriptions.isAlmost.GetHashCode() : 0);
            hash = hash * 23 + (aiConditionDescriptions.isAlmostFeedback != null ? aiConditionDescriptions.isAlmostFeedback.GetHashCode() : 0);
            hash = hash * 23 + aiConditionDescriptions.doGiveAlmostFeedback.GetHashCode();

            for (int i = 0; i < tasks.Count; i++)
            {
                var t = tasks[i];
                hash = hash * 23 + (t.header != null ? t.header.GetHashCode() : 0);
                hash = hash * 23 + (t.body != null ? t.body.GetHashCode() : 0);
                hash = hash * 23 + (t.placeholder != null ? t.placeholder.GetHashCode() : 0);
                hash = hash * 23 + (t.answerKey != null ? t.answerKey.GetHashCode() : 0);
                hash = hash * 23 + t.gradingSettings.GetHashCode();

                hash = hash * 23 + (int)t.taskType;

                var sl = t.singleLineSettings;
                hash = hash * 23 + sl.caseSensitive.GetHashCode();
                hash = hash * 23 + sl.checkMode.GetHashCode();
                hash = hash * 23 + (sl.leftText != null ? sl.leftText.GetHashCode() : 0);
                hash = hash * 23 + (sl.rightText != null ? sl.rightText.GetHashCode() : 0);
                hash = hash * 23 + sl.inputFitMode.GetHashCode();
                hash = hash * 23 + sl.minWidth.GetHashCode();
                hash = hash * 23 + sl.maxWidth.GetHashCode();
                hash = hash * 23 + sl.parentPaddingSide.GetHashCode();

                var ml = t.multiLineSettings;
                hash = hash * 23 + ml.checkMode.GetHashCode();
                hash = hash * 23 + ml.allowAIThinking.GetHashCode();

                hash = hash * 23 + t.useParts.GetHashCode();
                if (t.parts != null)
                {
                    hash = hash * 23 + t.parts.Count;
                    for (int p = 0; p < t.parts.Count; p++)
                    {
                        var part = t.parts[p];
                        hash = hash * 23 + (part.label != null ? part.label.GetHashCode() : 0);
                        hash = hash * 23 + (part.body != null ? part.body.GetHashCode() : 0);
                        hash = hash * 23 + (part.placeholder != null ? part.placeholder.GetHashCode() : 0);
                        hash = hash * 23 + (part.answerKey != null ? part.answerKey.GetHashCode() : 0);
                        hash = hash * 23 + (int)part.taskType;

                        var psl = part.singleLineSettings;
                        hash = hash * 23 + psl.caseSensitive.GetHashCode();
                        hash = hash * 23 + psl.checkMode.GetHashCode();
                        hash = hash * 23 + (psl.leftText != null ? psl.leftText.GetHashCode() : 0);
                        hash = hash * 23 + (psl.rightText != null ? psl.rightText.GetHashCode() : 0);
                        hash = hash * 23 + psl.inputFitMode.GetHashCode();
                        hash = hash * 23 + psl.minWidth.GetHashCode();
                        hash = hash * 23 + psl.maxWidth.GetHashCode();
                        hash = hash * 23 + psl.parentPaddingSide.GetHashCode();

                        var pml = part.multiLineSettings;
                        hash = hash * 23 + pml.checkMode.GetHashCode();
                        hash = hash * 23 + pml.allowAIThinking.GetHashCode();

                        hash = hash * 23 + part.doOverrideParentPadding.GetHashCode();
                        hash = hash * 23 + part.progress.GetHashCode();
                    }
                }

                hash = hash * 23 + t.doOverrideParentPadding.GetHashCode();

                hash = hash * 23 + (t.solutionSprites != null ? t.solutionSprites.Length : 0);

                hash = hash * 23 + t.hasLeftTask.GetHashCode();
                hash = hash * 23 + t.hasRightTask.GetHashCode();
                hash = hash * 23 + t.progress.GetHashCode();
            }

            return hash;
        }
    }

    void ForceSelectorsToZero()
    {
        if (selectorGroups == null) return;
        foreach (var grp in selectorGroups)
        {
            var sel = grp.selector;
            if (sel == null) continue;
            sel.saveSelected = false;
            sel.defaultIndex = 0;
            sel.index = 0;
        }
        _currentIndex = 0;
        UpdatePrevNextForAllSelectors();
    }
}