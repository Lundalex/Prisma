using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[Serializable]
public class SpecialMessagePreset
{
    public string presetName;
    [TextArea] public string visText;
    [TextArea] public string sendText;
    public bool spacialMessage = false;
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

    [Header("Communication")]
    [Tooltip("Optional: settings bundle (instructions/context/docs) used to build the combined prompt.")]
    [SerializeField] private CommunicationSettings communicationSettings;

    [Header("Placeholders")]
    [Tooltip("Shown in the assistant bubble while waiting for the answer.")]
    [SerializeField] private string assistantThinkingText = "…";

    [Header("Events")]
    [Tooltip("Invoked EVERY time text is applied to a message bubble (user, assistant, streaming updates, errors, etc).")]
    public UnityEvent onMessageTextSet;  // no parameters

    [Header("Scroll")]
    public Scrollbar scrollBar;

    [Header("Resilience")]
    [Tooltip("Max attempts for message retrieval.")]
    [SerializeField] private int maxAttempts = 3;

    [Tooltip("Text shown if all attempts fail.")]
    [SerializeField] private string errorFallbackText = "Fel: kunde inte hämta svar.";

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
        // Classic behavior: visible == sent, regular prefab
        HandleUserMessage(userMessage);
    }

    public void OnUserMessageRecieved(string userMessage) => HandleUserMessage(userMessage);
    public void OnUserMessageReceived(string userMessage) => HandleUserMessage(userMessage);

    /// <summary>
    /// NEW public API: Send a message by preset name. The preset defines visText, sendText, and spacialMessage.
    /// Also calls Expand() on the configured RectExpandMinimizeController (if any).
    /// </summary>
    /// <param name="specialMessagePresetName">Name of the preset defined in the inspector.</param>
    public void SendPresetUserMessage(string specialMessagePresetName)
    {
        var preset = FindPresetByName(specialMessagePresetName);
        if (preset == null)
        {
            Debug.LogWarning($"[AssistantChatManager] No SpecialMessagePreset found with name '{specialMessagePresetName}'. Aborting SendPresetUserMessage.");
            return;
        }

        // Fire expand on send (if assigned)
        if (expandMinimizeController != null)
            expandMinimizeController.Expand();

        _ = HandleUserMessageCore(preset.visText ?? string.Empty, preset.sendText ?? string.Empty, preset.spacialMessage);
    }

    /// <summary>
    /// Clears the chat: stops any ongoing streaming and removes all message bubbles under messageList.
    /// Also resets the scrollbar size to 1 on the next frame to avoid layout overwrites.
    /// </summary>
    public void ClearChat()
    {
        // Stop any ongoing stream first so callbacks stop touching destroyed objects.
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

        // Destroy all child message objects.
        for (int i = messageList.childCount - 1; i >= 0; i--)
        {
            var child = messageList.GetChild(i);
            if (child == null) continue;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(child.gameObject);
            else
#endif
                Destroy(child.gameObject);
        }

        // Refresh UI layout.
        Canvas.ForceUpdateCanvases();

        // Reset scrollbar size to 1 on the NEXT frame (layout rebuilds can overwrite same-frame changes)
        if (scrollBar != null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In edit mode there is no next-frame coroutine; set immediately.
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

    // Core handler used by both entry points; preserves old external behavior (visible == sent, regular prefab).
    private async void HandleUserMessage(string userMessage)
    {
        // Fire expand on send (if assigned)
        if (expandMinimizeController != null)
            expandMinimizeController.Expand();

        await HandleUserMessageCore(userMessage ?? string.Empty, userMessage ?? string.Empty, false);
    }

    // Shared implementation used by both the classic and preset-based APIs.
    private async System.Threading.Tasks.Task HandleUserMessageCore(string visText, string sendText, bool spacialMessage)
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
            var userPrefabToUse = ResolveUserPrefab(spacialMessage);
            var userMsgFallback = Instantiate(userPrefabToUse, messageList);
            userMsgFallback.name = $"UserMessage_{Time.frameCount}";
            userMsgFallback.SetStretchOrigin(userStretchOrigin);
            ApplyText(userMsgFallback, visText);

            var assistantPrefabToUse = messagePrefab ?? specialMessagePrefab;
            var assistantMsgFallback = Instantiate(assistantPrefabToUse, messageList);
            assistantMsgFallback.name = $"AssistantMessage_{Time.frameCount}";
            assistantMsgFallback.SetStretchOrigin(assistantStretchOrigin);
            ApplyText(assistantMsgFallback, "Assistant saknas i scenen (tag 'SmartAssistant').");

            Debug.LogError("[AssistantChatManager] Could not find SmartAssistant (tag 'SmartAssistant').");
            return;
        }

        // Build prompt using the CommunicationSettings (if assigned) and the *sendText* (NOT visText)
        var promptToSend = assistant.BuildPrompt(communicationSettings, sendText);

        // Create user message bubble (show the desired *visible* text)
        var userPrefab = ResolveUserPrefab(spacialMessage);
        var userMsg = Instantiate(userPrefab, messageList);
        userMsg.name = $"UserMessage_{Time.frameCount}";
        userMsg.SetStretchOrigin(userStretchOrigin);
        ApplyText(userMsg, visText);

        // Create assistant placeholder bubble (regular prefab for assistant)
        var assistantPrefab = messagePrefab ?? specialMessagePrefab;
        var assistantMsg = Instantiate(assistantPrefab, messageList);
        assistantMsg.name = $"AssistantMessage_{Time.frameCount}";
        assistantMsg.SetStretchOrigin(assistantStretchOrigin);
        ApplyText(assistantMsg, string.IsNullOrEmpty(assistantThinkingText) ? "" : assistantThinkingText);

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
                        string finalAnswer = await assistant.SendMessageStreamToCallbackAsync(
                            promptToSend,
                            onPartialText: (partial) =>
                            {
                                if (assistantMsg != null)
                                {
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
                        string answer = await assistant.SendMessageAsync(
                            promptToSend,
                            model: openAIModel,
                            allowThinking: false,
                            ct: attemptCts.Token
                        );
                        ApplyText(assistantMsg, answer ?? "");
                    }

                    success = true;
                    break; // exit retry loop
                }
                catch (OperationCanceledException oce)
                {
                    lastEx = oce;
                    Debug.LogError($"[AssistantChatManager] Attempt {attempt} canceled.");
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

    private StreamingMessage ResolveUserPrefab(bool spacialMessage)
    {
        // If special requested but missing, fall back to regular and warn.
        if (spacialMessage)
        {
            if (specialMessagePrefab != null) return specialMessagePrefab;
            if (messagePrefab != null)
            {
                Debug.LogWarning("[AssistantChatManager] specialMessagePrefab not assigned; falling back to messagePrefab for user bubble.");
                return messagePrefab;
            }
            // If both missing, return null (handled by caller earlier).
            return null;
        }
        // Regular path
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

    /// <summary>Centralized helper to set text, fit, and fire the public event (no args).</summary>
    private void ApplyText(StreamingMessage msg, string text)
    {
        if (msg == null) return;

        msg.SetText(text ?? string.Empty);
        msg.FitImmediate();

        onMessageTextSet?.Invoke();
    }

    /// <summary>
    /// Waits until the next frame (and end-of-frame) before applying the scrollbar size.
    /// This avoids layout/ContentSizeFitter updates overwriting the value.
    /// </summary>
    private IEnumerator SetScrollbarSizeNextFrame(float targetSize)
    {
        // Next frame
        yield return null;
        // End of that frame for extra safety
        yield return new WaitForEndOfFrame();

        if (scrollBar != null)
        {
            scrollBar.size = targetSize;
        }

        _scrollbarResetRoutine = null;
    }
}