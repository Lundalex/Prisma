using System;
using System.Collections;
using System.Threading;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AssistantChatManager : MonoBehaviour
{
    [Header("Prefabs & Parents")]
    [SerializeField] private StreamingMessage messagePrefab;
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

    public void OnUserMessageRecieved(string userMessage) => HandleUserMessage(userMessage);
    public void OnUserMessageReceived(string userMessage) => HandleUserMessage(userMessage);

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

    // Core handler used by both entry points
    private async void HandleUserMessage(string userMessage)
    {
        if (messagePrefab == null || messageList == null)
        {
            Debug.LogError("[AssistantChatManager] messagePrefab or messageList is not assigned.");
            return;
        }

        if (assistant == null)
        {
            assistant = SmartAssistant.FindByTagOrNull();
            if (assistant == null)
            {
                var userMsgFallback = Instantiate(messagePrefab, messageList);
                userMsgFallback.name = $"UserMessage_{Time.frameCount}";
                userMsgFallback.SetStretchOrigin(userStretchOrigin);
                ApplyText(userMsgFallback, userMessage ?? string.Empty);

                var assistantMsgFallback = Instantiate(messagePrefab, messageList);
                assistantMsgFallback.name = $"AssistantMessage_{Time.frameCount}";
                assistantMsgFallback.SetStretchOrigin(assistantStretchOrigin);
                ApplyText(assistantMsgFallback, "Assistant saknas i scenen (tag 'SmartAssistant').");

                Debug.LogError("[AssistantChatManager] Could not find SmartAssistant (tag 'SmartAssistant').");
                return;
            }
        }

        // Build prompt using the CommunicationSettings (if assigned)
        var promptToSend = assistant.BuildPrompt(communicationSettings, userMessage);

        // Create user message bubble (show raw user message, not the combined prompt)
        var userMsg = Instantiate(messagePrefab, messageList);
        userMsg.name = $"UserMessage_{Time.frameCount}";
        userMsg.SetStretchOrigin(userStretchOrigin);
        ApplyText(userMsg, userMessage ?? string.Empty);

        // Create assistant placeholder bubble
        var assistantMsg = Instantiate(messagePrefab, messageList);
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