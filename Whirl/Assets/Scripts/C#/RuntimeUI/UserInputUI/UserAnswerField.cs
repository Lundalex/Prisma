using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Michsky.MUIP;

[ExecuteInEditMode]
public class UserAnswerField : MonoBehaviour
{
    private const string TagSmartAssistant = "SmartAssistant"; 

    [Header("Answer Settings")]
    public string answerKey;
    [Tooltip("Used ONLY when mode is StringCompare.")] 
    public bool caseSensitive = false; 

    [Tooltip("Choose how to judge the answer: via AI or simple string comparison.")] 
    [SerializeField] private CheckMode checkMode = CheckMode.AI; 

    // AI-COM: --- Begin AI fields (auto-resolved SmartAssistant; not set via Inspector) ---
    [Header("AI")] 
    [Tooltip("CommunicationSettings passed as the System profile to the AI when judging answers.")] 
    [SerializeField] private CommunicationSettings gradingCommunicationSettings;

    [Tooltip("If true, enables extra reasoning effort on models that support it.")]
    [SerializeField] private bool allowAiReasoning = false; 

    private SmartAssistant smartAssistant;
    // AI-COM: --- End AI fields ---

    [Header("Colors")]
    [SerializeField, ColorUsage(true, true)] private Color defaultColor = Color.gray;
    [SerializeField, ColorUsage(true, true)] private Color editColor = Color.blue;
    [SerializeField, ColorUsage(true, true)] private Color successColor = Color.green;
    [SerializeField, ColorUsage(true, true)] private Color failColor = Color.red;
    [Tooltip("Shown when the AI deems the answer is on the right track but missing something important.")]
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
    private enum CheckMode { AI, StringCompare } 

    Color _outlineBaseColor;
    Coroutine _shakeCo, _flashCo;

    bool _editingOverride;
    Verdict _verdict = Verdict.None;
    string _lastSubmittedText = "";
    bool _dirtySinceSubmit = false;
    bool _prevEditing = false;
    bool _lostFocusAfterSubmit = false;

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

    // AI-COM: Now supports enum toggle. Uses AI or string compare depending on 'checkMode'.
    public async void ProcessAnswer() 
    {
        string answer = inputField.text;

        if (outlineObject != null) outlineObject.SetActive(true);

        bool answerIsCorrect = false; 
        bool answerIsAlmost = false;

        if (checkMode == CheckMode.StringCompare) 
        {
            answerIsCorrect = CompareWithAnswerKey(answer, answerKey); 
            answerIsAlmost = false; // StringCompare has no 'almost' concept
        }
        else // AI mode 
        {
            // Ensure assistant is available (e.g., if scene reloaded) 
            if (smartAssistant == null) ResolveSmartAssistant(); 

            if (smartAssistant == null) 
            {
                Debug.LogError("[UserAnswerField] SmartAssistant not found in scene (tag 'SmartAssistant'). Cannot validate via AI."); 
                answerIsCorrect = false; 
                answerIsAlmost = false;
            }
            else 
            {
                try 
                {
                    // AI-COM: Build a strict grading prompt.
                    var prompt =
$@"You are grading a single short-answer submission.

Primary rule:
- If the CommunicationSettings profile provides domain-specific grading rules, follow those.
- Otherwise, default grading is STRICT equality with the 'answer_key' after trimming leading/trailing whitespace.

Inputs:
- answer_key: ""{answerKey ?? ""}""
- user_answer: ""{answer ?? ""}""

Decide if the submission should be marked correct.

Additionally, return a brief feedback string suitable for the learner (e.g., 'Correct!' or 'Not quite. Expected ...').

If the CommunicationSettings defines criteria for a special 'on the right track but missing something important' state, set that state accordingly.";

                    // AI-COM: Define the condition spec the AI must return.
                    var specs = new System.Collections.Generic.List<AiConditionSpec> 
                    {
                        new AiConditionSpec 
                        {
                            key = "is_correct", 
                            instruction = "Return true iff the user_answer should be marked correct according to the rules. Else false.", 
                            type = CondType.Bool 
                        },
                        new AiConditionSpec
                        {
                            key = "is_almost",
                            instruction = "Return true iff the answer is on the right track but misses an important requirement per the CommunicationSettings. Else false.",
                            type = CondType.Bool
                        }
                    };

                    // AI-COM: Non-streaming call; returns full JSON at once.
                    var (aiText, conds) = await smartAssistant.SendMessageWithAiConditionsAsync( 
                        prompt,                                                       
                        specs,                                                         
                        gradingCommunicationSettings,                                 
                        model: null,                                                  
                        allowThinking: allowAiReasoning                               
                    );                                                                

                    // AI-COM: Extract booleans with robust coercion fallbacks.
                    if (conds != null && conds.TryGetValue("is_correct", out var v) && v is bool b1) 
                        answerIsCorrect = b1; 
                    else 
                        answerIsCorrect = false; 

                    if (conds != null && conds.TryGetValue("is_almost", out var v2) && v2 is bool b2)
                        answerIsAlmost = b2;
                    else
                        answerIsAlmost = false;
                }
                catch (System.Exception ex) 
                {
                    Debug.LogError($"[UserAnswerField] AI validation failed: {ex.Message}"); 
                    answerIsCorrect = false; 
                    answerIsAlmost = false;
                }
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

            // Fire event (e.g., AnimatedPopupIcon.Play)
            onCorrect?.Invoke();

            // Optionally switch window immediately
            if (submitNextWindowManager != null)
            {
                submitNextWindowManager.OpenWindow(nextWindowName);
            }
        }
        else if (answerIsAlmost)
        {
            // Acts like Fail in logic (flash + shake, no window switch, edit flow identical),
            // but displays ALMOST color (yellow).
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashOutline(almostColor));

            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeObject());

            _verdict = Verdict.Almost;
            _lastSubmittedText = answer;
            _dirtySinceSubmit = false;
            _lostFocusAfterSubmit = false;
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

    // AI-COM: Brought back for StringCompare mode only.
    protected virtual bool CompareWithAnswerKey(string answer, string key) 
    {
        if (answer == null || key == null) return false; 
        if (caseSensitive) return answer == key; 
        return string.Equals(answer, key, StringComparison.OrdinalIgnoreCase); 
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