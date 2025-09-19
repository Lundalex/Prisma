using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Michsky.MUIP;

[ExecuteInEditMode]
public class UserAnswerField : MonoBehaviour
{
    const string TagSmartAssistant = "SmartAssistant";
    const string TagChatManager = "ChatManager";
    const string TagTaskManager = "TaskManager";
    const string almostHeader = "<size=160%><b><u>Nästan!</u></b></size>";

    [Header("Answer Settings")]
    public string answerKey;
    public bool caseSensitive = false;
    [SerializeField] private CheckMode checkMode = CheckMode.StringCompareThenAI;

    [Header("AI")]
    [SerializeField] private CommunicationSettings gradingCommunicationSettings;
    [SerializeField] private bool allowAIThinking = false;

    [Header("AI Condition Instructions")]
    [SerializeField] private string isCorrectInstructions =
        "Return true if the user_answer should be marked correct according to the rules. Else false.";
    [SerializeField] private string isAlmostInstructions =
        "Return true if the answer is 'almost' (on the right track but missing something important) per the CommunicationSettings. Else false.";
    [SerializeField] private string almostFeedbackInstructions =
        "If is_almost is true, return a SHORT, actionable hint guiding the user to the correct answer. Otherwise return an empty string.";
    string almostHeaderInstructions =
        "If is_almost is true, return the HEADER text. Use the provided baseline header but translate it into the language of user_answer if that language differs from Swedish; otherwise return it unchanged. Keep it concise (max ~5 words).";

    [SerializeField] private bool postAlmostChatMessage = true;

    AssistantChatManager chatManager;

    [Header("Colors")]
    [SerializeField, ColorUsage(true, true)] private Color defaultColor = Color.gray;
    [SerializeField, ColorUsage(true, true)] private Color editColor = Color.blue;
    [SerializeField, ColorUsage(true, true)] private Color successColor = Color.green;
    [SerializeField, ColorUsage(true, true)] private Color failColor = Color.red;
    [SerializeField, ColorUsage(true, true)] private Color almostColor = Color.yellow;

    [Header("Outline (UI)")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject outlineObject;

    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;
    [SerializeField] private TMP_Text placeholder;

    [Header("Window Manager")]
    [SerializeField] private WindowManager submitNextWindowManager;
    [SerializeField] private string submitWindowName;
    [SerializeField] private string nextWindowName;

    [Header("Fail / Almost Feedback")]
    [SerializeField] private float shakePixels = 8f;
    [SerializeField] private float shakeCycleDuration = 0.14f;
    [SerializeField] private int shakeCycles = 3;
    [SerializeField] private float outlineLerp = 0.06f;

    [SerializeField] private RectTransform _rt;

    [Header("Events (legacy - not used on success)")]
    public UnityEvent onCorrect;

    SmartAssistant smartAssistant;
    Color _outlineBaseColor;
    Coroutine _shakeCo, _flashCo;

    bool _editingOverride;
    Verdict _verdict = Verdict.None;
    string _lastSubmittedText = "";
    bool _dirtySinceSubmit;
    bool _prevEditing;
    bool _lostFocusAfterSubmit;
    bool _isEvaluating;

    [Header("Links")]
    [SerializeField] private Task task;
    [SerializeField] private TaskManager taskManager;

    [Header("Correct Color Targets")]
    [SerializeField] private Image[] correctColorImages;

    public bool IsAnimating => _shakeCo != null || _flashCo != null;

    void Awake()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (outlineImage != null)
        {
            _outlineBaseColor = defaultColor;
            outlineImage.color = defaultColor;
        }
        if (submitNextWindowManager != null) submitNextWindowManager.OpenWindow(submitWindowName);

        ResolveTaskAndManager();
        ResolveSmartAssistant();
        ResolveChatManager();
    }

    void OnDisable()
    {
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        if (_flashCo != null) StopCoroutine(_flashCo);

        if (_rt != null) _rt.anchoredPosition = Vector2.zero;
        if (outlineImage != null) outlineImage.color = _outlineBaseColor;

        _editingOverride = false;
        _dirtySinceSubmit = false;
        _prevEditing = false;
        _lostFocusAfterSubmit = false;
        _isEvaluating = false;
    }

    void Update()
    {
        if (outlineImage == null) return;

        bool editing = TMPInputChecker.UserIsUsingInputField(inputField);

        if (_verdict == Verdict.None)
        {
            if (editing)
            {
                if (!_editingOverride && _flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
                if (outlineObject != null) outlineObject.SetActive(true);
                outlineImage.color = editColor;
                _editingOverride = true;
            }
            else
            {
                if (_editingOverride || outlineImage.color == editColor)
                {
                    outlineImage.color = defaultColor;
                    _editingOverride = false;
                }
            }
        }
        else if (_verdict == Verdict.Success)
        {
            if (outlineImage.color != successColor) outlineImage.color = successColor;
        }
        else
        {
            if (!_prevEditing && editing && _lostFocusAfterSubmit)
            {
                outlineImage.color = editColor;
                _editingOverride = true;
            }

            if (editing && !_dirtySinceSubmit && inputField.text != _lastSubmittedText)
            {
                outlineImage.color = editColor;
                _dirtySinceSubmit = true;
                _editingOverride = true;
            }
            else if (!editing && outlineImage.color == editColor)
            {
                outlineImage.color = defaultColor;
                _editingOverride = false;
            }
        }

        if (_verdict != Verdict.None && _prevEditing && !editing) _lostFocusAfterSubmit = true;
        _prevEditing = editing;
    }

    public async void ProcessAnswer()
    {
        if (_isEvaluating) return;
        _isEvaluating = true;

        try
        {
            string answer = inputField.text;
            if (outlineObject != null) outlineObject.SetActive(true);

            bool answerIsCorrect = false;
            bool answerIsAlmost = false;
            string almostFeedbackText = null;
            string almostHeaderFromAi = null;

            if (checkMode == CheckMode.StringCompare)
            {
                answerIsCorrect = CompareWithAnswerKey(answer, answerKey);
            }
            else if (checkMode == CheckMode.AI)
            {
                var aiRes = await EvaluateWithAI(answer);
                answerIsCorrect = aiRes.isCorrect;
                answerIsAlmost = aiRes.isAlmost;
                almostFeedbackText = aiRes.almostFeedback;
                almostHeaderFromAi = aiRes.almostHeader;
            }
            else
            {
                answerIsCorrect = CompareWithAnswerKey(answer, answerKey);
                if (!answerIsCorrect)
                {
                    var aiRes = await EvaluateWithAI(answer);
                    answerIsCorrect = aiRes.isCorrect;
                    answerIsAlmost = aiRes.isAlmost;
                    almostFeedbackText = aiRes.almostFeedback;
                    almostHeaderFromAi = aiRes.almostHeader;
                }
            }

            if (answerIsCorrect)
            {
                if (_flashCo != null) StopCoroutine(_flashCo);
                outlineImage.color = successColor;
                _verdict = Verdict.Success;
                _lastSubmittedText = answer;
                _dirtySinceSubmit = false;
                _lostFocusAfterSubmit = false;

                NotifyTaskManagerCorrect();

                if (submitNextWindowManager != null)
                    submitNextWindowManager.OpenWindow(nextWindowName);
            }
            else if (answerIsAlmost)
            {
                if (_flashCo != null) StopCoroutine(_flashCo);
                _flashCo = StartCoroutine(FlashOutline(almostColor));

                if (_shakeCo != null) StopCoroutine(_shakeCo);
                _shakeCo = StartCoroutine(ShakeObject());

                _verdict = Verdict.Almost;
                _lastSubmittedText = answer;
                _dirtySinceSubmit = false;
                _lostFocusAfterSubmit = false;

                if (postAlmostChatMessage)
                {
                    if (chatManager == null) ResolveChatManager();

                    if (chatManager != null)
                    {
                        var headerOut = SanitizeHeader(almostHeaderFromAi ?? almostHeader);
                        var bodyOut = string.IsNullOrWhiteSpace(almostFeedbackText)
                            ? "You're almost there—review the instructions and add the missing element(s)."
                            : almostFeedbackText.Trim();

                        chatManager.PostAssistantSpecialMessage(headerOut, bodyOut);
                    }
                }
            }
            else
            {
                if (_flashCo != null) StopCoroutine(_flashCo);
                _flashCo = StartCoroutine(FlashOutline(failColor));

                if (_shakeCo != null) StopCoroutine(_shakeCo);
                _shakeCo = StartCoroutine(ShakeObject());

                _verdict = Verdict.Fail;
                _lastSubmittedText = answer;
                _dirtySinceSubmit = false;
                _lostFocusAfterSubmit = false;
            }
        }
        finally
        {
            _isEvaluating = false;
        }
    }

    protected virtual bool CompareWithAnswerKey(string answer, string key)
    {
        if (answer == null || key == null) return false;
        return caseSensitive ? (answer == key)
                             : string.Equals(answer, key, StringComparison.OrdinalIgnoreCase);
    }

    public void ApplyColors(Color normal, Color edit, Color success, Color fail, Color almost)
    {
        defaultColor = normal;
        editColor = edit;
        successColor = success;
        failColor = fail;
        almostColor = almost;

        if (correctColorImages != null)
        {
            for (int i = 0; i < correctColorImages.Length; i++)
            {
                var img = correctColorImages[i];
                if (img == null) continue;
                var c = img.color;
                img.color = new Color(successColor.r, successColor.g, successColor.b, c.a);
            }
        }

        if (outlineImage != null && _verdict == Verdict.None && !_editingOverride)
            outlineImage.color = defaultColor;
    }

    public void ApplyFeedback(float shakePixels, float shakeCycleDuration, int shakeCycles, float outlineLerp)
    {
        this.shakePixels = shakePixels;
        this.shakeCycleDuration = shakeCycleDuration;
        this.shakeCycles = shakeCycles;
        this.outlineLerp = outlineLerp;
    }

    public void SetPlaceholder(string text)
    {
        if (placeholder != null) placeholder.text = text ?? string.Empty;
    }

    public void SetCaseSensitive(bool value) => caseSensitive = value;
    public void SetCheckMode(CheckMode mode) => checkMode = mode;
    public void SetAllowAIThinking(bool value) => allowAIThinking = value;
    public void SetGradingSettings(CommunicationSettings settings) => gradingCommunicationSettings = settings;

    public void SetAIInstructions(string isCorrect, string isAlmost, string almostFeedback, bool postAlmostToChat, string customAlmostHeaderInstruction = null)
    {
        if (!string.IsNullOrWhiteSpace(isCorrect)) isCorrectInstructions = isCorrect;
        if (!string.IsNullOrWhiteSpace(isAlmost)) isAlmostInstructions = isAlmost;
        if (!string.IsNullOrWhiteSpace(almostFeedback)) almostFeedbackInstructions = almostFeedback;
        if (!string.IsNullOrWhiteSpace(customAlmostHeaderInstruction)) almostHeaderInstructions = customAlmostHeaderInstruction;
        postAlmostChatMessage = postAlmostToChat;
    }

    public void ApplyProgressState(Verdict v)
    {
        if (_shakeCo != null) { StopCoroutine(_shakeCo); _shakeCo = null; }
        if (_flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
        if (_rt != null) _rt.anchoredPosition = Vector2.zero;

        _verdict = v;
        _editingOverride = false;
        _dirtySinceSubmit = false;
        _lostFocusAfterSubmit = false;

        if (outlineObject != null) outlineObject.SetActive(true);

        if (outlineImage != null)
        {
            var c = defaultColor;
            switch (v)
            {
                case Verdict.Success: c = successColor; break;
                case Verdict.Fail:    c = failColor;    break;
                case Verdict.Almost:  c = almostColor;  break;
                case Verdict.None:    c = defaultColor; break;
            }
            outlineImage.color = c;
        }
    }

    async Task<(bool isCorrect, bool isAlmost, string almostFeedback, string almostHeader)> EvaluateWithAI(string answer)
    {
        bool isCorrect = false, isAlmost = false;
        string feedback = null, header = null;

        if (smartAssistant == null) ResolveSmartAssistant();
        if (smartAssistant == null) return (false, false, null, null);

        var prompt =
$@"You are grading a single short-answer submission.

Primary rule:
- If the CommunicationSettings profile provides domain-specific grading rules, follow those.
- Otherwise, default grading is STRICT equality with the 'answer_key' after trimming leading/trailing whitespace.

Inputs:
- answer_key: ""{answerKey ?? ""}""
- user_answer: ""{answer ?? ""}""
- baseline_almost_header: ""{almostHeader ?? ""}""

Decide:
- is_correct: true/false
- is_almost: true/false

Output policy:
- If is_almost is false: almost_feedback = """" and almost_header = """";
- If is_almost is true:
  * almost_feedback: SHORT, actionable hint. Plain text only.
  * almost_header: 1–3 words; use baseline header but translate if needed.";

        var specs = new List<AiConditionSpec>
        {
            new AiConditionSpec { key = "is_correct",       instruction = isCorrectInstructions,       type = CondType.Bool },
            new AiConditionSpec { key = "is_almost",        instruction = isAlmostInstructions,        type = CondType.Bool },
            new AiConditionSpec { key = "almost_feedback",  instruction = almostFeedbackInstructions,  type = CondType.String },
            new AiConditionSpec { key = "almost_header",    instruction = almostHeaderInstructions,    type = CondType.String }
        };

        var (aiText, conds) = await smartAssistant.SendMessageWithAiConditionsAsync(
            prompt, specs, gradingCommunicationSettings, model: null, allowThinking: allowAIThinking
        );

        if (conds != null && conds.TryGetValue("is_correct", out var v) && v is bool b1) isCorrect = b1;
        if (conds != null && conds.TryGetValue("is_almost", out var v2) && v2 is bool b2) isAlmost = b2;

        if (conds != null && conds.TryGetValue("almost_feedback", out var v3))
        {
            feedback = v3 as string ?? v3?.ToString();
            if (string.IsNullOrWhiteSpace(feedback)) feedback = null;
        }

        if (conds != null && conds.TryGetValue("almost_header", out var v4))
        {
            header = v4 as string ?? v4?.ToString();
            if (string.IsNullOrWhiteSpace(header)) header = null;
        }

        if (isAlmost)
        {
            if (string.IsNullOrWhiteSpace(feedback))
                feedback = string.IsNullOrWhiteSpace(aiText)
                    ? "You're almost there—review the requirements and address the missing part."
                    : aiText.Trim();

            if (string.IsNullOrWhiteSpace(header))
                header = string.IsNullOrWhiteSpace(almostHeader) ? "Almost" : almostHeader.Trim();
        }

        return (isCorrect, isAlmost, feedback, header);
    }

    void ResolveTaskAndManager()
    {
        if (task == null) task = GetComponentInParent<Task>();

        if (taskManager == null)
        {
            if (task != null && task.TaskManager != null) taskManager = task.TaskManager;
            if (taskManager == null)
            {
                var go = GameObject.FindGameObjectWithTag(TagTaskManager);
                if (go != null) taskManager = go.GetComponent<TaskManager>();
            }
        }
    }

    void NotifyTaskManagerCorrect()
    {
        if (taskManager == null || task == null) ResolveTaskAndManager();
        if (taskManager != null && task != null) taskManager.OnAnswerFieldCorrect(task);
    }

    void ResolveSmartAssistant()
    {
        smartAssistant = null;
        var objs = GameObject.FindGameObjectsWithTag(TagSmartAssistant);
        if (objs != null && objs.Length > 0)
            smartAssistant = objs[0].GetComponent<SmartAssistant>();
    }

    void ResolveChatManager()
    {
        chatManager = null;
        try
        {
            var objs = GameObject.FindGameObjectsWithTag(TagChatManager);
            if (objs != null && objs.Length > 0)
                chatManager = objs[0].GetComponent<AssistantChatManager>();
        }
        catch { }
    }

    string SanitizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return "Almost";
        header = header.Trim();
        return header.Replace("\r", " ").Replace("\n", " ");
    }

    IEnumerator FlashOutline(Color target)
    {
        if (outlineImage == null) yield break;

        float t = 0f;
        Color from = outlineImage.color;
        while (t < outlineLerp)
        {
            t += Time.unscaledDeltaTime;
            outlineImage.color = Color.Lerp(from, target, t / Mathf.Max(0.0001f, outlineLerp));
            yield return null;
        }

        outlineImage.color = target;
        _flashCo = null;
    }

    IEnumerator ShakeObject()
    {
        if (_rt == null) yield break;

        Vector2 basePos = _rt.anchoredPosition;

        for (int i = 0; i < shakeCycles; i++)
        {
            float d = Mathf.Max(0.0001f, shakeCycleDuration);
            float t = 0f;

            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float f = Mathf.Clamp01(t / d);

                float x;
                if (f < 0.25f) x = Mathf.Lerp(0f, +shakePixels, f / 0.25f);
                else if (f < 0.75f) x = Mathf.Lerp(+shakePixels, -shakePixels, (f - 0.25f) / 0.5f);
                else x = Mathf.Lerp(-shakePixels, 0f, (f - 0.75f) / 0.25f);

                _rt.anchoredPosition = basePos + new Vector2(x, 0f);
                yield return null;
            }
        }

        _rt.anchoredPosition = basePos;
        _shakeCo = null;
    }
}

public enum CheckMode { AI, StringCompare, StringCompareThenAI }
public enum Verdict { None, Success, Fail, Almost }