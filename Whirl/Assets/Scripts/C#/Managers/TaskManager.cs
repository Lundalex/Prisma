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
    [SerializeField] private List<UserTask> tasks = new();

    [Header("Main (Workspace) Windows")]
    public WindowManager workspaceWindowManager;
    public GameObject taskWindowPrefab;

    [Header("Side Windows")]
    public WindowManager sideWindowManager;
    public GameObject sideTaskWindowPrefab;

    [Header("Side Pane Layout")]
    public DualMultiContainer dualMultiContainer;

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

    [Header("Chat")]
    [SerializeField] private AssistantChatManager assistantChatManager;

    [Header("Fullscreen")]
    [SerializeField] private GameObject fullscreenView;

    [SerializeField, HideInInspector] private List<GameObject> taskWindowGOs = new();
    [SerializeField, HideInInspector] private List<Task> taskScripts = new();
    [SerializeField, HideInInspector] private List<GameObject> sideTaskWindowGOs = new();
    [SerializeField, HideInInspector] private List<SideTask> sideTaskScripts = new();

    int _currentIndex;
    bool _didRuntimeInit;
    Coroutine _consistencyLoop;

    public void OpenFullscreenView()
    {
        if (fullscreenView != null && !fullscreenView.activeSelf) fullscreenView.SetActive(true);
    }

    public void GoToPrevTask()
    {
        if (tasks == null || tasks.Count == 0) return;
        int next = Mathf.Max(0, _currentIndex - 1);
        if (next != _currentIndex) OpenTaskByIndex(next);
    }

    public void GoToNextTask()
    {
        if (tasks == null || tasks.Count == 0) return;
        int next = Mathf.Min(tasks.Count - 1, _currentIndex + 1);
        if (next != _currentIndex) OpenTaskByIndex(next);
    }

    void OnChanged()
    {
        SetWorkspaceTaskWindows();
        SetSideTaskWindows();
        SyncDualMultiContainerTargets();
        ApplyTaskDataToScripts();
        BuildTaskSelectorItems();
        ApplyProgressToAllIndicators();
        UpdatePrevNextForAllSelectors();

        // No global container flipping here (prevents cross-task layout bleed).
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

        for (int i = 0; i < tasks.Count; i++) CreateWorkspaceTaskWindow(i, existingByIndex);
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

        for (int i = 0; i < tasks.Count; i++) CreateSideTaskWindow(i, existingByIndex);
    }

    // ---------- Apply data to Task & SideTask ----------
    void ApplyTaskDataToScripts()
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

            var grow = view.SingleLineAutoGrow != null
                ? view.SingleLineAutoGrow
                : view.GetComponentInChildren<AutoGrowToText>(true);

            if (grow != null)
            {
                grow.SetLeftText(t.singleLineSettings.leftText);
                grow.SetRightText(t.singleLineSettings.rightText);
                grow.SetPlaceholder(t.placeholder);
                grow.SetFitMode(t.singleLineSettings.inputFitMode);
                if (t.singleLineSettings.inputFitMode == InputFitMode.Absolute)
                {
                    float min = Mathf.Max(1f, t.singleLineSettings.minWidth);
                    float max = Mathf.Max(min, t.singleLineSettings.maxWidth);
                    grow.SetMinMaxWidth(min, max);
                }
                else
                {
                    grow.SetParentPadding(Mathf.Max(0f, t.singleLineSettings.parentPadding));
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
                        f.answerKey = t.answerKey;
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

                        if (f is UserMultiLineAnswerField)
                        {
                            f.SetCheckMode(t.multiLineSettings.checkMode);
                            f.SetAllowAIThinking(t.multiLineSettings.allowAIThinking);
                        }
                        else
                        {
                            f.SetCaseSensitive(t.singleLineSettings.caseSensitive);
                            f.SetCheckMode(t.singleLineSettings.checkMode);
                        }

                        f.SetGradingSettings(t.gradingSettings);
                        var aic = t.aiConditionDescriptions;
                        f.SetAIInstructions(aic.isCorrect, aic.isAlmost, aic.isAlmostFeedback, aic.doGiveAlmostFeedback);

                        // reflect saved progress immediately
                        f.ApplyProgressState(t.progress);
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

            var grow = view.SingleLineAutoGrow != null
                ? view.SingleLineAutoGrow
                : view.GetComponentInChildren<AutoGrowToText>(true);

            if (grow != null)
            {
                grow.SetLeftText(t.singleLineSettings.leftText);
                grow.SetRightText(t.singleLineSettings.rightText);
                grow.SetPlaceholder(t.placeholder);
                grow.SetFitMode(t.singleLineSettings.inputFitMode);
                if (t.singleLineSettings.inputFitMode == InputFitMode.Absolute)
                {
                    float min = Mathf.Max(1f, t.singleLineSettings.minWidth);
                    float max = Mathf.Max(min, t.singleLineSettings.maxWidth);
                    grow.SetMinMaxWidth(min, max);
                }
                else
                {
                    grow.SetParentPadding(Mathf.Max(0f, t.singleLineSettings.parentPadding));
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
                        f.answerKey = t.answerKey;
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

                        if (f is UserMultiLineAnswerField)
                        {
                            f.SetCheckMode(t.multiLineSettings.checkMode);
                            f.SetAllowAIThinking(t.multiLineSettings.allowAIThinking);
                        }
                        else
                        {
                            f.SetCaseSensitive(t.singleLineSettings.caseSensitive);
                            f.SetCheckMode(t.singleLineSettings.checkMode);
                        }

                        f.SetGradingSettings(t.gradingSettings);
                        var aic = t.aiConditionDescriptions;
                        f.SetAIInstructions(aic.isCorrect, aic.isAlmost, aic.isAlmostFeedback, aic.doGiveAlmostFeedback);

                        // reflect saved progress immediately
                        f.ApplyProgressState(t.progress);
                    }
                }
            }
        }
    }

    // ---------- DualMultiContainer <-> SideTask targets ----------
    void SyncDualMultiContainerTargets()
    {
        if (dualMultiContainer == null) return;

        int n = tasks.Count;
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

        // --- IMPORTANT ---
        // Editor: force the neutral (multiline) base layout to avoid accidental singleline stretch bleed.
        // Runtime: let the container initialize normally.
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            dualMultiContainer.SetStretchTargetAlt(false); // base = multiline
        }
        else
        {
            dualMultiContainer.InitDisplay();
        }
#else
        dualMultiContainer.InitDisplay();
#endif
    }

    void BuildTaskSelectorItems()
    {
        if (selectorGroups == null || selectorGroups.Length == 0) return;

        int count = tasks.Count;
        int prevIndex = Mathf.Clamp(_currentIndex, 0, Mathf.Max(0, count - 1));

        for (int s = 0; s < selectorGroups.Length; s++)
        {
            var sel = selectorGroups[s].selector;
            if (sel == null) continue;

            sel.saveSelected = false;

            var newItems = new List<HorizontalSelector.Item>(count);
            for (int i = 0; i < count; i++)
            {
                int capturedIndex = i;
                var item = new HorizontalSelector.Item { itemTitle = $"{selectorItemPrefix}{capturedIndex + 1}" };
                item.onItemSelect.AddListener(() => OpenTaskByIndex(capturedIndex));
                newItems.Add(item);
            }

            sel.items = newItems;
            sel.defaultIndex = prevIndex;
            sel.index = prevIndex;

            sel.onValueChanged.RemoveAllListeners();
            sel.onValueChanged.AddListener(OpenTaskByIndex);

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                if (sel.label != null && sel.items.Count > 0)
                    sel.label.text = sel.items[sel.index].itemTitle;
                if (sel.labelHelper != null)
                    sel.labelHelper.text = sel.label != null ? sel.label.text :
                        (sel.items.Count > 0 ? sel.items[sel.index].itemTitle : string.Empty);
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
        // Still no global container flipping here.
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

    // ---------- Progress <-> Indicators ----------
    void ApplyProgressToIndicators(SelectorGroup group)
    {
        if (group.indicators == null) return;

        for (int i = 0; i < tasks.Count; i++)
        {
            if (tasks[i].progress == Verdict.Success) group.indicators.SetIndicatorMarked(i);
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
        for (int i = 0; i < selectorGroups.Length; i++)
        {
            var grp = selectorGroups[i];
            if (grp.indicators == null || grp.selector == null) continue;
            grp.indicators.SetPrevNext(grp.selector.index, tasks.Count);
        }
    }

    public void OnAnswerFieldCorrect(Task task)
    {
        if (task == null) return;

        int idx = taskScripts.IndexOf(task);
        bool isWorkspaceTask = idx >= 0;
        if (idx < 0 && task is SideTask st) idx = sideTaskScripts.IndexOf(st);
        if (idx >= 0) SetTaskProgress(idx, Verdict.Success);

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
        if (idx < 0 || idx >= tasks.Count - 1) return;
        OpenTaskByIndex(idx + 1);
    }

    public void OpenTaskByIndex(int index)
    {
        if (index < 0 || index >= tasks.Count) return;
        _currentIndex = index;

        if (workspaceWindowManager != null) workspaceWindowManager.OpenWindowByIndex(index);
        if (sideWindowManager != null) sideWindowManager.OpenWindowByIndex(index);

        if (selectorGroups != null)
        {
            for (int s = 0; s < selectorGroups.Length; s++)
            {
                var sel = selectorGroups[s].selector;
                if (sel == null) continue;

                sel.index = index;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    if (sel.label != null && sel.items.Count > 0)
                        sel.label.text = sel.items[index].itemTitle;

                    if (sel.labelHelper != null)
                        sel.labelHelper.text = sel.label != null
                            ? sel.label.text
                            : (sel.items.Count > 0 ? sel.items[index].itemTitle : string.Empty);
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
        // No global container flip here either.
    }

    public void SendTip()
    {
        if (assistantChatManager != null) assistantChatManager.SendPresetUserMessage("Tip");
    }

    void Awake() => UpdateNeighbors();

    void OnEnable()
    {
        if (Application.isPlaying) LoadProgressIfAvailable();
        UpdateNeighbors();

#if UNITY_EDITOR
        EditorApplication.playModeStateChanged -= HandleEditorPlayModeChanged;
        EditorApplication.playModeStateChanged += HandleEditorPlayModeChanged;
#endif

        if (Application.isPlaying && !_didRuntimeInit)
        {
            _didRuntimeInit = true;

            ForceSelectorsToZero();
            SetWorkspaceTaskWindows();
            SetSideTaskWindows();
            SyncDualMultiContainerTargets();
            ApplyTaskDataToScripts();
            BuildTaskSelectorItems();

            OpenTaskByIndex(0);
            ApplyProgressToAllIndicators();
            ApplyProgressToAllAnswerFields();

            if (_consistencyLoop == null) _consistencyLoop = StartCoroutine(ConsistencyHeartbeat());
        }
        else
        {
            ApplyProgressToAllIndicators();
            UpdatePrevNextForAllSelectors();
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
        if (Application.isPlaying) SaveProgress();
    }

    IEnumerator ConsistencyHeartbeat()
    {
        var wait = new WaitForSeconds(1f);
        while (true)
        {
            if (selectorGroups != null)
            {
                for (int i = 0; i < selectorGroups.Length; i++)
                {
                    var sel = selectorGroups[i].selector;
                    if (sel == null) continue;
                    if (sel.index != _currentIndex)
                    {
                        sel.index = _currentIndex;
                        sel.UpdateUI();
                    }
                }
            }

            ApplyProgressToAllIndicators();
            ApplyProgressToAllAnswerFields();
            UpdatePrevNextForAllSelectors();
            SaveProgress();
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

    public void SetTaskProgress(int taskIndex, Verdict progress)
    {
        if (taskIndex < 0 || taskIndex >= tasks.Count) return;
        var t = tasks[taskIndex];
        t.progress = progress;
        tasks[taskIndex] = t;

        if (selectorGroups != null)
        {
            for (int i = 0; i < selectorGroups.Length; i++)
            {
                var ind = selectorGroups[i].indicators;
                if (ind == null) continue;

                if (progress == Verdict.Success) ind.SetIndicatorMarked(taskIndex);
                else ind.SetIndicatorUnmarked(taskIndex);
            }
        }

        ApplyProgressToTaskIndex(taskIndex);
        if (Application.isPlaying) SaveProgress();
    }

    public void SetAllTaskProgress(Verdict progress)
    {
        for (int i = 0; i < tasks.Count; i++)
        {
            var t = tasks[i];
            t.progress = progress;
            tasks[i] = t;
        }

        ApplyProgressToAllIndicators();
        ApplyProgressToAllAnswerFields();
        if (Application.isPlaying) SaveProgress();
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

                if (selectorGroups != null)
                {
                    for (int g = 0; g < selectorGroups.Length; g++)
                    {
                        var ind = selectorGroups[g].indicators;
                        if (ind == null) continue;

                        if (progress == Verdict.Success) ind.SetIndicatorMarked(i);
                        else ind.SetIndicatorUnmarked(i);
                    }
                }

                ApplyProgressToTaskIndex(i);
                if (Application.isPlaying) SaveProgress();
                break;
            }
        }
    }

    // ---------- Progress -> AnswerFields helpers ----------
    void ApplyProgressToTaskIndex(int i)
    {
        if (i < 0 || i >= tasks.Count) return;
        var v = tasks[i].progress;

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
    }

    void ApplyProgressToAllAnswerFields()
    {
        for (int i = 0; i < tasks.Count; i++) ApplyProgressToTaskIndex(i);
    }

    // ---------- Progress persistence (session-only) ----------
    void SaveProgress()
    {
        if (progressStorage == null) return;
        var arr = new int[tasks.Count];
        for (int i = 0; i < tasks.Count; i++) arr[i] = (int)tasks[i].progress;
        progressStorage.SetValue(arr);
    }

    void LoadProgressIfAvailable()
    {
        if (progressStorage == null || !DataStorage.hasValue) return;
        var loaded = progressStorage.GetValue<int[]>();
        if (loaded == null || loaded.Length == 0) return;

        int n = Mathf.Min(tasks.Count, loaded.Length);
        for (int i = 0; i < n; i++)
        {
            var t = tasks[i];
            t.progress = (Verdict)Mathf.Clamp(loaded[i], 0, (int)Verdict.Almost);
            tasks[i] = t;
        }
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
    enum TaskType { SingleLine, MultiLine }

    [System.Serializable]
    struct AIConditionDescriptions
    {
        [TextArea(2, 40)] public string isCorrect, isAlmost, isAlmostFeedback;
        public bool doGiveAlmostFeedback;
    }

    [System.Serializable]
    struct SingleLineSettings
    {
        public bool caseSensitive;
        public CheckMode checkMode;
        public string leftText, rightText;
        public InputFitMode inputFitMode;
        public float minWidth;
        public float maxWidth;
        public float parentPadding;
    }

    [System.Serializable]
    struct MultiLineSettings
    {
        public CheckMode checkMode;
        public bool allowAIThinking;
    }

    [System.Serializable]
    struct UserTask
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
    class UserTaskDrawer : PropertyDrawer
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

        static float GetH(SerializedProperty root, string rel)
        {
            var p = root.FindPropertyRelative(rel);
            return p == null ? EditorGUIUtility.singleLineHeight : EditorGUI.GetPropertyHeight(p, true);
        }

        static float Draw(Rect pos, float y, string rel, SerializedProperty root)
        {
            var p = root.FindPropertyRelative(rel);
            if (p == null) return y;
            float h = EditorGUI.GetPropertyHeight(p, true);
            EditorGUI.PropertyField(new Rect(pos.x, y, pos.width, h), p, true);
            return y + h + EditorGUIUtility.standardVerticalSpacing;
        }
    }

    [CustomPropertyDrawer(typeof(SingleLineSettings))]
    class SingleLineSettingsDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
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
                h += line("parentPadding") + v;
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
                EditorGUI.PropertyField(Row(EditorGUI.GetPropertyHeight(property.FindPropertyRelative("parentPadding"), true)),
                    property.FindPropertyRelative("parentPadding"));
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
        double now = EditorApplication.timeSinceStartup;
        if (now < _nextCheckTime) return;
        _nextCheckTime = now + 0.1f;

        int h = ComputeStateHash();
        if (h != _lastHash)
        {
            _lastHash = h;
            OnChanged();
        }
    }

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
                hash = hash * 23 + sl.inputFitMode.GetHashCode();
                hash = hash * 23 + sl.minWidth.GetHashCode();
                hash = hash * 23 + sl.maxWidth.GetHashCode();
                hash = hash * 23 + sl.parentPadding.GetHashCode();

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

    void HandleEditorPlayModeChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode) ResetSelectorsToFirstInEditor();
    }

    void ResetSelectorsToFirstInEditor()
    {
        if (tasks == null || tasks.Count == 0) return;

        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            ForceSelectorsToZero();
            OpenTaskByIndex(0);
            ApplyProgressToAllIndicators();
        };
    }
#endif
}