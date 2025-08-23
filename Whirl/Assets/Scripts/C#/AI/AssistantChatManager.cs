// ─────────────────────────────────────────────────────────────────────────────
// AssistantChatManager.cs
// - Adds overrideSpecialMarking: force user=normal, assistant=special (overrides presets)
// - Fixes "spacialMessage" -> "specialMessage" (kept with FormerlySerializedAs for compatibility)
// - Adds firstResponseTimeoutSeconds under Resilience:
//     * streaming: cancel & retry if no partial arrives before timeout
//     * non-streaming: cancel & retry if full answer not returned before 2x timeout
// ─────────────────────────────────────────────────────────────────────────────
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using UnityEngine.Serialization;

[Serializable]
public class SpecialMessagePreset
{
    public string presetName;
    [TextArea] public string visText;
    [TextArea] public string sendText;

    // Typo fixed + backward compatibility for existing scenes
    [FormerlySerializedAs("spacialMessage")]
    public bool specialMessage = false;

    [Tooltip("If true, the USER bubble created from this preset will animate its text (type-on).")]
    public bool streamAnimation = false;
}

public class AssistantChatManager : MonoBehaviour
{
    [Header("Prefabs & Parents")]
    [SerializeField] private StreamingMessage messagePrefab;
    [SerializeField] private StreamingMessage specialMessagePrefab;
    [SerializeField] private RectTransform messageList;

    [Header("Message Sides")]
    [SerializeField] private StreamingMessage.StretchOrigin userStretchOrigin = StreamingMessage.StretchOrigin.Right;
    [SerializeField] private StreamingMessage.StretchOrigin assistantStretchOrigin = StreamingMessage.StretchOrigin.Left;

    [Header("Input Source")]
    [Tooltip("Reference to the UserAssistantField that raises a no-arg onSend UnityEvent and exposes lastSentMessage.")]
    [SerializeField] private UserAssistantField userAssistantField;

    [Header("Assistant")]
    [Tooltip("If true, answers will stream token-by-token into the assistant bubble.")]
    [SerializeField] private bool responseStreaming = true;
    [SerializeField] private string openAIModel = "gpt-4o";

    [Header("Special Message (AI 'Almost')")]
    [Tooltip("If true, the AI's special 'almost' message (header + feedback) will animate with a type-on effect.")]
    [SerializeField] private bool streamAlmostAssistantMessage = false;

    [Header("Communication")]
    [Tooltip("Optional: settings bundle (instructions/context/docs) used to build the combined prompt.")]
    [SerializeField] private CommunicationSettings communicationSettings;

    [Header("Placeholders")]
    [Tooltip("Shown in the assistant bubble while waiting for the answer.")]
    [SerializeField] private string assistantThinkingText = "…";

    [Header("Message Style Overrides")]
    [Tooltip("If enabled: all USER messages are normal bubbles; all ASSISTANT messages are special bubbles. Overrides presets.")]
    [SerializeField] private bool overrideSpecialMarking = false;

    [Header("Resilience")]
    [Tooltip("Max attempts for message retrieval.")]
    [SerializeField] private int maxAttempts = 3;

    [Tooltip("Seconds to wait for the FIRST streamed token. If none arrives in time, cancel & retry. Non-streaming calls use 2x this value.")]
    [Min(0.25f)]
    [SerializeField] private float firstResponseTimeoutSeconds = 6f;

    [Tooltip("Text shown if all attempts fail.")]
    [SerializeField] private string errorFallbackText = "Fel: kunde inte hämta svar.";

    [Header("Events")]
    [Tooltip("Invoked EVERY time text is applied to a message bubble (user, assistant, streaming updates, errors, etc).")]
    public UnityEvent onMessageTextSet;  // no parameters

    [Header("Scroll")]
    public Scrollbar scrollBar;

    [Header("Expand/Minimize")]
    [Tooltip("Optional. Will call Expand() every time a message is sent.")]
    [SerializeField] private RectExpandMinimizeController expandMinimizeController;

    [Header("Special Message Presets")]
    [Tooltip("Lookup table for SendPresetUserMessage(presetName). Matching is case-insensitive and trims whitespace.")]
    [SerializeField] private SpecialMessagePreset[] specialMessagePresets;

    private SmartAssistant assistant;
    private CancellationTokenSource streamCts;
    private Coroutine _scrollbarResetRoutine;

    void Awake()
    {
        assistant = SmartAssistant.FindByTagOrNull();
        if (assistant == null)
            Debug.LogWarning("[AssistantChatManager] No SmartAssistant found via tag 'SmartAssistant' at Awake(). Will try again on first message.");
    }

    /// <summary>Hook this to UserAssistantField.onSend (no parameters).</summary>
    public void OnUserSend()
    {
        if (userAssistantField == null)
        {
            Debug.LogError("[AssistantChatManager] userAssistantField is not assigned.");
            return;
        }
        var userMessage = userAssistantField.lastSentMessage ?? string.Empty;
        HandleUserMessage(userMessage);
    }

    // Kept for compatibility
    public void OnUserMessageRecieved(string userMessage) => HandleUserMessage(userMessage);
    public void OnUserMessageReceived(string userMessage) => HandleUserMessage(userMessage);

    /// <summary>
    /// Send a message by preset name. The preset defines visText, sendText, specialMessage, and streamAnimation.
    /// Also calls Expand() on the configured RectExpandMinimizeController (if any).
    /// </summary>
    public void SendPresetUserMessage(string specialMessagePresetName)
    {
        var preset = FindPresetByName(specialMessagePresetName);
        if (preset == null)
        {
            Debug.LogWarning($"[AssistantChatManager] No SpecialMessagePreset found with name '{specialMessagePresetName}'. Aborting SendPresetUserMessage.");
            return;
        }

        if (expandMinimizeController != null)
            expandMinimizeController.Expand();

        _ = HandleUserMessageCore(
            visText: preset.visText ?? string.Empty,
            sendText: preset.sendText ?? string.Empty,
            specialMessageRequested: preset.specialMessage,
            userStreamAnimation: preset.streamAnimation
        );
    }

    /// <summary>
    /// PUBLIC API: Post a SPECIAL assistant message (header + body).
    /// Header may come from AI (possibly translated). Streaming is controlled by 'streamAlmostAssistantMessage'.
    /// Always uses SPECIAL style (and also when overrideSpecialMarking is enabled).
    /// </summary>
    public void PostAssistantSpecialMessage(string header, string body)
    {
        if (messageList == null)
        {
            Debug.LogError("[AssistantChatManager] PostAssistantSpecialMessage called but messageList is not assigned.");
            return;
        }

        if (expandMinimizeController != null)
            expandMinimizeController.Expand();

        var prefab = (specialMessagePrefab != null ? specialMessagePrefab : messagePrefab);
        if (prefab == null)
        {
            Debug.LogError("[AssistantChatManager] Neither specialMessagePrefab nor messagePrefab is assigned.");
            return;
        }

        var msg = Instantiate(prefab, messageList);
        msg.name = $"AssistantSpecialMessage_{Time.frameCount}";
        msg.SetStretchOrigin(assistantStretchOrigin);

        header = string.IsNullOrWhiteSpace(header) ? "Note" : header.Trim();
        body = (body ?? string.Empty).Trim();

        string combined = string.IsNullOrEmpty(body) ? header : $"{header}\n\n{body}";

        // Configure streaming for the special message
        msg.SetStreamOptions(streamAlmostAssistantMessage);
        if (streamAlmostAssistantMessage) msg.SetText(combined);
        else msg.SetTextImmediate(combined);

        onMessageTextSet?.Invoke();
    }

    /// <summary>Clears all chat bubbles and resets the scrollbar on the next frame.</summary>
    public void ClearChat()
    {
        if (streamCts != null)
        {
            try { streamCts.Cancel(); } catch { }
            streamCts.Dispose();
            streamCts = null;
        }

        if (messageList == null)
        {
            Debug.LogWarning("[AssistantChatManager] ClearChat called but messageList is not assigned.");
            return;
        }

        for (int i = messageList.childCount - 1; i >= 0; i--)
        {
            var child = messageList.GetChild(i);
            if (child == null) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                UnityEngine.Object.DestroyImmediate(child.gameObject);
            else
#endif
                UnityEngine.Object.Destroy(child.gameObject);
        }

        Canvas.ForceUpdateCanvases();

        if (scrollBar != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                scrollBar.size = 1f;
            }
            else
#endif
            {
                if (_scrollbarResetRoutine != null) StopCoroutine(_scrollbarResetRoutine);
                _scrollbarResetRoutine = StartCoroutine(SetScrollbarSizeNextFrame(1f));
            }
        }
    }

    // ----- Internal routing (kept for backward compatibility with existing hooks) -----

    private async void HandleUserMessage(string userMessage)
    {
        if (expandMinimizeController != null)
            expandMinimizeController.Expand();

        await HandleUserMessageCore(userMessage ?? string.Empty, userMessage ?? string.Empty, false, false);
    }

    // Shared implementation used by both the classic and preset-based APIs.
    private async System.Threading.Tasks.Task HandleUserMessageCore(
        string visText,
        string sendText,
        bool specialMessageRequested,
        bool userStreamAnimation)
    {
        if ((messagePrefab == null && specialMessagePrefab == null) || messageList == null)
        {
            Debug.LogError("[AssistantChatManager] messagePrefab/specialMessagePrefab or messageList is not assigned.");
            return;
        }

        if (assistant == null)
        {
            assistant = SmartAssistant.FindByTagOrNull();

            // Even if assistant is missing, still render the UI messages to keep UX consistent.
            var userPrefabToUse = ResolveUserPrefab(specialMessageRequested);
            var userMsgFallback = Instantiate(userPrefabToUse, messageList);
            userMsgFallback.name = $"UserMessage_{Time.frameCount}";
            userMsgFallback.SetStretchOrigin(userStretchOrigin);
            userMsgFallback.SetStreamOptions(userStreamAnimation);
            if (userStreamAnimation) userMsgFallback.SetText(visText ?? string.Empty);
            else userMsgFallback.SetTextImmediate(visText ?? string.Empty);

            var assistantPrefabToUse = ResolveAssistantPrefab();
            var assistantMsgFallback = Instantiate(assistantPrefabToUse, messageList);
            assistantMsgFallback.name = $"AssistantMessage_{Time.frameCount}";
            assistantMsgFallback.SetStretchOrigin(assistantStretchOrigin);
            ApplyText(assistantMsgFallback, "Assistant saknas i scenen (tag 'SmartAssistant').");

            Debug.LogError("[AssistantChatManager] Could not find SmartAssistant (tag 'SmartAssistant').");
            return;
        }

        // Build prompt using the CommunicationSettings (if assigned) and the *sendText* (NOT visText)
        var promptToSend = assistant.BuildPrompt(communicationSettings, sendText);

        // Create user message bubble (visible text)
        var userPrefab = ResolveUserPrefab(specialMessageRequested);
        var userMsg = Instantiate(userPrefab, messageList);
        userMsg.name = $"UserMessage_{Time.frameCount}";
        userMsg.SetStretchOrigin(userStretchOrigin);
        userMsg.SetStreamOptions(userStreamAnimation);
        if (userStreamAnimation) userMsg.SetText(visText ?? string.Empty);
        else userMsg.SetTextImmediate(visText ?? string.Empty);

        // Create assistant placeholder bubble
        var assistantPrefab = ResolveAssistantPrefab();
        var assistantMsg = Instantiate(assistantPrefab, messageList);
        assistantMsg.name = $"AssistantMessage_{Time.frameCount}";
        assistantMsg.SetStretchOrigin(assistantStretchOrigin);
        ApplyText(assistantMsg, string.IsNullOrEmpty(assistantThinkingText) ? "" : assistantThinkingText);

        // Cancel any prior attempt
        streamCts?.Cancel();
        streamCts?.Dispose();
        streamCts = new CancellationTokenSource();

        bool success = false;
        Exception lastEx = null;

        for (int attempt = 1; attempt <= Mathf.Max(1, maxAttempts); attempt++)
        {
            using (var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(streamCts.Token))
            {
                try
                {
                    if (responseStreaming)
                    {
                        bool beganStreaming = false;

                        // Watchdog: if no partial arrives in time, cancel attempt and retry
                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            try
                            {
                                int ms = Mathf.Max(250, Mathf.RoundToInt(firstResponseTimeoutSeconds * 1000f));
                                await System.Threading.Tasks.Task.Delay(ms).ConfigureAwait(false);
                                if (!beganStreaming && !attemptCts.IsCancellationRequested)
                                    attemptCts.Cancel();
                            }
                            catch { /* ignore */ }
                        });

                        string finalAnswer = await assistant.SendMessageStreamToCallbackAsync(
                            promptToSend,
                            onPartialText: (partial) =>
                            {
                                beganStreaming = true;
                                if (assistantMsg != null)
                                {
                                    // Assistant bubble text updates instantly (visual animation is handled by StreamingMessage, if any)
                                    ApplyText(assistantMsg, partial ?? string.Empty);
                                }
                            },
                            model: openAIModel,
                            allowThinking: false,
                            ct: attemptCts.Token
                        );

                        ApplyText(assistantMsg, finalAnswer ?? string.Empty);
                    }
                    else
                    {
                        // Non-streaming watchdog: total timeout = 2x firstResponseTimeoutSeconds
                        int ms = Mathf.Max(500, Mathf.RoundToInt(firstResponseTimeoutSeconds * 2000f));
                        var answerTask = assistant.SendMessageAsync(
                            promptToSend,
                            model: openAIModel,
                            allowThinking: false,
                            ct: attemptCts.Token
                        );

                        var timeoutTask = System.Threading.Tasks.Task.Delay(ms);
                        var completed = await System.Threading.Tasks.Task.WhenAny(answerTask, timeoutTask);
                        if (completed == timeoutTask)
                        {
                            // Cancel and let retry loop handle next attempt
                            attemptCts.Cancel();
                            throw new OperationCanceledException("Non-streaming response timed out.");
                        }

                        string answer = await answerTask; // propagate exceptions if any
                        ApplyText(assistantMsg, answer ?? "");
                    }

                    success = true;
                    break; // exit retry loop
                }
                catch (OperationCanceledException oce)
                {
                    lastEx = oce;
                    Debug.LogWarning($"[AssistantChatManager] Attempt {attempt} canceled (timeout or external cancel).");
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    Debug.LogError($"[AssistantChatManager] Attempt {attempt} failed: {ex.Message}");
                }
            }
        }

        if (!success)
        {
            Debug.LogError($"[AssistantChatManager] All {Mathf.Max(1, maxAttempts)} attempts failed. Last error: {lastEx?.Message}");
            ApplyText(assistantMsg, errorFallbackText);
        }
    }

    private SpecialMessagePreset FindPresetByName(string name)
    {
        if (specialMessagePresets == null || specialMessagePresets.Length == 0) return null;
        if (string.IsNullOrWhiteSpace(name)) return null;

        var trimmed = name.Trim();
        for (int i = 0; i < specialMessagePresets.Length; i++)
        {
            var p = specialMessagePresets[i];
            if (p != null && !string.IsNullOrEmpty(p.presetName) &&
                string.Equals(p.presetName.Trim(), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return p;
            }
        }
        return null;
    }

    private StreamingMessage ResolveUserPrefab(bool specialMessageRequested)
    {
        // If override is ON, user messages are ALWAYS normal
        if (overrideSpecialMarking)
            return messagePrefab ?? specialMessagePrefab;

        // Otherwise, respect the request (fall back gracefully)
        if (specialMessageRequested)
        {
            if (specialMessagePrefab != null) return specialMessagePrefab;
            if (messagePrefab != null)
            {
                Debug.LogWarning("[AssistantChatManager] specialMessagePrefab not assigned; falling back to messagePrefab for user bubble.");
                return messagePrefab;
            }
            return null;
        }
        // Regular path
        return messagePrefab ?? specialMessagePrefab;
    }

    private StreamingMessage ResolveAssistantPrefab()
    {
        // If override is ON, assistant messages are ALWAYS special
        if (overrideSpecialMarking)
            return specialMessagePrefab != null ? specialMessagePrefab : messagePrefab;

        // Otherwise: default assistant style is regular (falls back to special if missing)
        return messagePrefab ?? specialMessagePrefab;
    }

    private void OnDestroy()
    {
        streamCts?.Cancel();
        streamCts?.Dispose();
        streamCts = null;

        if (_scrollbarResetRoutine != null)
        {
            StopCoroutine(_scrollbarResetRoutine);
            _scrollbarResetRoutine = null;
        }
    }

    /// <summary>Centralized helper to set text instantly (assistant & special messages that aren't animated).</summary>
    private void ApplyText(StreamingMessage msg, string text)
    {
        if (msg == null) return;

        msg.SetTextImmediate(text ?? string.Empty);
        onMessageTextSet?.Invoke();
    }

    private IEnumerator SetScrollbarSizeNextFrame(float targetSize)
    {
        yield return null;
        yield return new WaitForEndOfFrame();

        if (scrollBar != null)
        {
            scrollBar.size = targetSize;
        }

        _scrollbarResetRoutine = null;
    }
}