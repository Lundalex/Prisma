// ─────────────────────────────────────────────────────────────────────────────
// UserAnswerField.cs
// - AI controls 'almost' header; may translate based on answer language
// - Auto-resolves AssistantChatManager by tag "ChatManager" (no Inspector ref)
// - Prevents re-submission while an evaluation is in progress
// - NEW CheckMode: StringCompareThenAI — try StringCompare first, then fall back to AI
// ─────────────────────────────────────────────────────────────────────────────
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
    private const string TagSmartAssistant = "SmartAssistant";
    private const string TagChatManager    = "ChatManager";
    private const string almostHeader = "<size=160%><b><u>Nästan!</u></b></size>";

    [Header("Answer Settings")]
    public string answerKey;
    [Tooltip("Used ONLY when mode is StringCompare.")]
    public bool caseSensitive = false;

    [Tooltip("Choose how to judge the answer: via AI, simple string comparison, or StringCompare then AI fallback.")]
    [SerializeField] private CheckMode checkMode = CheckMode.StringCompareThenAI;

    // AI-COM: --- Begin AI fields (auto-resolved SmartAssistant; not set via Inspector) ---
    [Header("AI")]
    [Tooltip("CommunicationSettings passed as the System profile to the AI when grading answers.")]
    [SerializeField] private CommunicationSettings gradingCommunicationSettings;

    [Tooltip("If true, enables extra reasoning effort on models that support it.")]
    [SerializeField] private bool allowAiReasoning = false;

    [Header("AI Condition Instructions")]
    [SerializeField] private string isCorrectInstructions =
        "Return true if the user_answer should be marked correct according to the rules. Else false.";
    [SerializeField] private string isAlmostInstructions =
        "Return true if the answer is 'almost' (on the right track but missing something important) per the CommunicationSettings. Else false.";
    [SerializeField] private string almostFeedbackInstructions =
        "If is_almost is true, return a SHORT, actionable hint guiding the user to the correct answer. Otherwise return an empty string.";
    [SerializeField] private string almostHeaderInstructions =
        "If is_almost is true, return the HEADER text. Use the provided baseline header but translate it into the language of user_answer if that language differs from Swedish; otherwise return it unchanged. Keep it concise (max ~5 words).";
    // AI-COM: --- End AI fields ---

    [Tooltip("If enabled, will post a special assistant message when verdict is 'almost'.")]
    [SerializeField] private bool postAlmostChatMessage = true;

    // Auto-resolved by tag; not set via Inspector
    private AssistantChatManager chatManager;

    [Header("Colors")]
    [SerializeField, ColorUsage(true, true)] private Color defaultColor = Color.gray;
    [SerializeField, ColorUsage(true, true)] private Color editColor = Color.blue;
    [SerializeField, ColorUsage(true, true)] private Color successColor = Color.green;
    [SerializeField, ColorUsage(true, true)] private Color failColor = Color.red;
    [Tooltip("Shown when the AI deems the answer is 'almost' correct but missing something important.")]
    [SerializeField, ColorUsage(true, true)] private Color almostColor = Color.yellow;

    [Header("Outline (UI)")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject outlineObject;

    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;

    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Window Manager")]
    [SerializeField] private WindowManager submitNextWindowManager;
    [SerializeField] private string submitWindowName;
    [SerializeField] private string nextWindowName;

    [Header("Fail / Almost Feedback")]
    [Tooltip("Shake amplitude in UI pixels (±X).")]
    [SerializeField] private float shakePixels = 8f;
    [Tooltip("Duration of a single shake cycle.")]
    [SerializeField] private float shakeCycleDuration = 0.14f;
    [Tooltip("How many shake cycles to play.")]
    [SerializeField] private int shakeCycles = 3;

    [Tooltip("Time to lerp to the target verdict color.")]
    [SerializeField] private float outlineLerp = 0.06f;

    [SerializeField] private RectTransform _rt;

    [Header("Events")]
    [Tooltip("Invoked when the submitted answer is correct. Bind AnimatedPopupIcon.Play() here.")]
    public UnityEvent onCorrect;

    private enum Verdict { None, Success, Fail, Almost }
    private enum CheckMode { AI, StringCompare, StringCompareThenAI }

    private SmartAssistant smartAssistant;
    Color _outlineBaseColor;
    Coroutine _shakeCo, _flashCo;

    bool _editingOverride;
    Verdict _verdict = Verdict.None;
    string _lastSubmittedText = "";
    bool _dirtySinceSubmit = false;
    bool _prevEditing = false;
    bool _lostFocusAfterSubmit = false;

    // Prevents re-submission while evaluation is running
    bool _isEvaluating = false;

    void Awake()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (outlineImage != null)
        {
            _outlineBaseColor = defaultColor;
            outlineImage.color = defaultColor;
        }

        if (submitNextWindowManager != null)
        {
            submitNextWindowManager.OpenWindow(submitWindowName);
        }

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
                if (!_editingOverride)
                {
                    if (_flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
                }
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
        else // Fail-like (Fail or Almost)
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

        if (_verdict != Verdict.None)
        {
            if (_prevEditing && !editing) _lostFocusAfterSubmit = true;
        }

        _prevEditing = editing;
    }

    // AI-COM: Uses AI, StringCompare, or StringCompareThenAI depending on 'checkMode'.
    public async void ProcessAnswer()
    {
        // Guard against double-submit
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
                // Pure string compare path
                answerIsCorrect = CompareWithAnswerKey(answer, answerKey);
                answerIsAlmost = false;
            }
            else if (checkMode == CheckMode.AI)
            {
                // Pure AI grading
                var aiRes = await EvaluateWithAI(answer);
                answerIsCorrect      = aiRes.isCorrect;
                answerIsAlmost       = aiRes.isAlmost;
                almostFeedbackText   = aiRes.almostFeedback;
                almostHeaderFromAi   = aiRes.almostHeader;
            }
            else // CheckMode.StringCompareThenAI
            {
                // Try fast string compare first; if false, fall back to AI
                answerIsCorrect = CompareWithAnswerKey(answer, answerKey);
                if (!answerIsCorrect)
                {
                    var aiRes = await EvaluateWithAI(answer);
                    answerIsCorrect      = aiRes.isCorrect;
                    answerIsAlmost       = aiRes.isAlmost;
                    almostFeedbackText   = aiRes.almostFeedback;
                    almostHeaderFromAi   = aiRes.almostHeader;
                }
                else
                {
                    answerIsAlmost = false;
                }
            }

            // ── Apply verdict UI/logic ─────────────────────────────────────────
            if (answerIsCorrect)
            {
                if (_flashCo != null) StopCoroutine(_flashCo);
                outlineImage.color = successColor;
                _verdict = Verdict.Success;
                _lastSubmittedText = answer;
                _dirtySinceSubmit = false;
                _lostFocusAfterSubmit = false;

                onCorrect?.Invoke();

                if (submitNextWindowManager != null)
                {
                    submitNextWindowManager.OpenWindow(nextWindowName);
                }
            }
            else if (answerIsAlmost)
            {
                // Fail-like behavior, but yellow + special message
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
                    else
                    {
                        Debug.LogWarning("[UserAnswerField] postAlmostChatMessage is enabled, but no AssistantChatManager with tag 'ChatManager' was found.");
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

    // AI-COM: Brought back for StringCompare mode only.
    protected virtual bool CompareWithAnswerKey(string answer, string key)
    {
        if (answer == null || key == null) return false;
        if (caseSensitive) return answer == key;
        return string.Equals(answer, key, StringComparison.OrdinalIgnoreCase);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helper: AI evaluation (shared by AI and StringCompareThenAI paths)
    // ─────────────────────────────────────────────────────────────────────────
    private async Task<(bool isCorrect, bool isAlmost, string almostFeedback, string almostHeader)> EvaluateWithAI(string answer)
    {
        bool isCorrect = false;
        bool isAlmost  = false;
        string feedback = null;
        string header   = null;

        if (smartAssistant == null) ResolveSmartAssistant();

        if (smartAssistant == null)
        {
            Debug.LogError("[UserAnswerField] SmartAssistant not found in scene (tag 'SmartAssistant'). Cannot validate via AI.");
            return (false, false, null, null);
        }

        try
        {
            var prompt =
$@"You are grading a single short-answer submission.

Primary rule:
- If the CommunicationSettings profile provides domain-specific grading rules, follow those.
- Otherwise, default grading is STRICT equality with the 'answer_key' after trimming leading/trailing whitespace.

Inputs:
- answer_key: ""{answerKey ?? ""}""
- user_answer: ""{answer ?? ""}""
- baseline_almost_header: ""{almostHeader ?? ""}""  (This is the baseline header text ONLY.)

Decide:
- is_correct: true/false
- is_almost: true/false (""almost"" = on right track but missing something important per CommunicationSettings)

Output policy (CRITICAL):
- If is_almost is false: almost_feedback = """" and almost_header = """" (both empty).
- If is_almost is true:
  * almost_feedback: SHORT, actionable hint. PLAIN TEXT ONLY. Do NOT include any header, title, rich-text tags, or the baseline_almost_header.
  * almost_header: A concise header (1–3 words). Use baseline_almost_header but translate into user_answer language if needed.
- Do NOT return any extra narrative outside the specified fields.";

            var specs = new List<AiConditionSpec>
            {
                new AiConditionSpec
                {
                    key = "is_correct",
                    instruction = isCorrectInstructions,
                    type = CondType.Bool
                },
                new AiConditionSpec
                {
                    key = "is_almost",
                    instruction = isAlmostInstructions,
                    type = CondType.Bool
                },
                new AiConditionSpec
                {
                    key = "almost_feedback",
                    instruction = almostFeedbackInstructions,
                    type = CondType.String
                },
                new AiConditionSpec
                {
                    key = "almost_header",
                    instruction = almostHeaderInstructions,
                    type = CondType.String
                }
            };

            var (aiText, conds) = await smartAssistant.SendMessageWithAiConditionsAsync(
                prompt,
                specs,
                gradingCommunicationSettings,
                model: null,
                allowThinking: allowAiReasoning
            );

            if (conds != null && conds.TryGetValue("is_correct", out var v) && v is bool b1)
                isCorrect = b1;
            else
                isCorrect = false;

            if (conds != null && conds.TryGetValue("is_almost", out var v2) && v2 is bool b2)
                isAlmost = b2;
            else
                isAlmost = false;

            if (conds != null && conds.TryGetValue("almost_feedback", out var v3))
            {
                feedback = v3 as string ?? v3?.ToString();
                if (string.IsNullOrWhiteSpace(feedback))
                    feedback = null;
            }

            if (conds != null && conds.TryGetValue("almost_header", out var v4))
            {
                header = v4 as string ?? v4?.ToString();
                if (string.IsNullOrWhiteSpace(header))
                    header = null;
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
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UserAnswerField] AI validation failed: {ex.Message}");
            return (false, false, null, null);
        }

        return (isCorrect, isAlmost, feedback, header);
    }

    private void ResolveSmartAssistant()
    {
        smartAssistant = null;

        var objs = GameObject.FindGameObjectsWithTag(TagSmartAssistant);
        if (objs == null || objs.Length == 0)
        {
            Debug.LogError($"[UserAnswerField] No GameObject with tag '{TagSmartAssistant}' found in the scene.");
            return;
        }

        if (objs.Length > 1)
        {
            Debug.LogWarning($"[UserAnswerField] Multiple GameObjects with tag '{TagSmartAssistant}' found. Using the first one: {objs[0].name}");
        }

        var comp = objs[0].GetComponent<SmartAssistant>();
        if (comp == null)
        {
            Debug.LogError($"[UserAnswerField] GameObject '{objs[0].name}' has tag '{TagSmartAssistant}' but no SmartAssistant component.");
            return;
        }

        smartAssistant = comp;
    }

    private void ResolveChatManager()
    {
        chatManager = null;

        GameObject[] objs;
        try
        {
            objs = GameObject.FindGameObjectsWithTag(TagChatManager);
        }
        catch (UnityException)
        {
            Debug.LogError($"[UserAnswerField] Tag '{TagChatManager}' is not defined in the Tag Manager.");
            return;
        }

        if (objs == null || objs.Length == 0) return;

        if (objs.Length > 1)
        {
            Debug.LogWarning($"[UserAnswerField] Multiple GameObjects with tag '{TagChatManager}' found. Using the first one: {objs[0].name}");
        }

        var comp = objs[0].GetComponent<AssistantChatManager>();
        if (comp == null)
        {
            Debug.LogError($"[UserAnswerField] GameObject '{objs[0].name}' has tag '{TagChatManager}' but no AssistantChatManager component.");
            return;
        }

        chatManager = comp;
    }

    private string SanitizeHeader(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return "Almost";
        header = header.Trim();
        // keep header to a single line
        header = header.Replace("\r", " ").Replace("\n", " ");
        return header;
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