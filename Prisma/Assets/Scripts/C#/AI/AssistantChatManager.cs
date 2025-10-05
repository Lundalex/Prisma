using System;
using System.Collections;
using System.Threading;
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

    [FormerlySerializedAs("spacialMessage")]
    public bool specialMessage = false;

    public bool streamAnimation = false;
}

public class AssistantChatManager : MonoBehaviour
{
    [Header("Prefabs & Parents")]
    [SerializeField] private StreamingMessage messagePrefab;
    [SerializeField] private StreamingMessage specialMessagePrefab;
    [SerializeField] private StreamingMessage initialMessagePrefab;
    [SerializeField] private RectTransform messageList;

    [Header("Message Sides")]
    [SerializeField] private StreamingMessage.StretchOrigin userStretchOrigin = StreamingMessage.StretchOrigin.Right;
    [SerializeField] private StreamingMessage.StretchOrigin assistantStretchOrigin = StreamingMessage.StretchOrigin.Left;

    [Header("Input Source")]
    [SerializeField] private UserAssistantField userAssistantField;

    [Header("Assistant")]
    [SerializeField] private bool responseStreaming = true;
    [SerializeField] private string openAIModel = "gpt-4o";

    [Header("Special Message (AI 'Almost')")]
    [SerializeField] private bool streamAlmostAssistantMessage = false;

    [Header("Communication")]
    [SerializeField] private CommunicationSettings communicationSettings;

    [Header("Placeholders")]
    [SerializeField] private string assistantThinkingText = "…";

    [Header("Message Style Overrides")]
    [SerializeField] private bool overrideSpecialMarking = false;

    [Header("Resilience")]
    [SerializeField] private int maxAttempts = 3;
    [Min(2.0f)]
    [SerializeField] private float firstResponseTimeoutSeconds = 6f;
    [SerializeField] private string errorFallbackText = "Fel: kunde inte hämta svar.";

    [Header("Events")]
    public UnityEvent onMessageTextSet;

    [Header("Scroll")]
    public Scrollbar scrollBar;

    [Header("Expand/Minimize")]
    [SerializeField] private RectExpandMinimizeController expandMinimizeController;

    [Header("Initial Message Behavior")]
    [SerializeField] private bool expandOnInitialAssistantMessage = false;

    [Header("Special Message Presets")]
    [SerializeField] private SpecialMessagePreset[] specialMessagePresets;

    private SceneManagementHeader sceneManagementHeader;
    private SmartAssistant assistant;
    private CancellationTokenSource streamCts;
    private Coroutine _scrollbarResetRoutine;
    private Coroutine _deferredExpandRoutine;

    void Awake()
    {
        assistant = SmartAssistant.FindByTagOrNull();
        if (assistant == null)
            Debug.LogWarning("[AssistantChatManager] No SmartAssistant found via tag 'SmartAssistant' at Awake(). Will try again on first message.");
    }

    void Start()
    {
        if (initialMessagePrefab == null)
        {
            Debug.LogWarning("initialMessagePrefab not assigned in inspector. Skipping sending an initial message");
            return;
        }

        if (messageList == null)
        {
            Debug.LogWarning("[AssistantChatManager] Cannot spawn initial assistant message: messageList is not assigned.");
            return;
        }

        if (expandOnInitialAssistantMessage)
            EnsureFullscreenAndExpanded();

        var msg = Instantiate(initialMessagePrefab, messageList);
        msg.name = "AssistantMessage_Initial";
        msg.SetStretchOrigin(assistantStretchOrigin);
        msg.SetStreamOptions(false);

        msg.transform.SetAsFirstSibling();

        onMessageTextSet?.Invoke();
    }

    public void OnUserSend()
    {
        if (userAssistantField == null)
        {
            Debug.LogError("[AssistantChatManager] userAssistantField is not assigned.");
            return;
        }
        var userMessage = userAssistantField.lastSentMessage ?? string.Empty;
        EnsureFullscreenAndExpanded();
        HandleUserMessage(userMessage);
    }

    public void OnUserMessageRecieved(string userMessage)
    {
        EnsureFullscreenAndExpanded();
        HandleUserMessage(userMessage);
    }
    public void OnUserMessageReceived(string userMessage)
    {
        EnsureFullscreenAndExpanded();
        HandleUserMessage(userMessage);
    }

    public void SendPresetUserMessage(string specialMessagePresetName)
    {
        var preset = FindPresetByName(specialMessagePresetName);
        if (preset == null)
        {
            Debug.LogWarning($"[AssistantChatManager] No SpecialMessagePreset found with name '{specialMessagePresetName}'. Aborting SendPresetUserMessage.");
            return;
        }

        EnsureFullscreenAndExpanded();

        _ = HandleUserMessageCore(
            visText: preset.visText ?? string.Empty,
            sendText: preset.sendText ?? string.Empty,
            specialMessageRequested: preset.specialMessage,
            userStreamAnimation: preset.streamAnimation
        );
    }

    public void PostAssistantSpecialMessage(string header, string body)
    {
        if (messageList == null)
        {
            Debug.LogError("[AssistantChatManager] PostAssistantSpecialMessage called but messageList is not assigned.");
            return;
        }

        EnsureFullscreenAndExpanded();

        var prefab = (specialMessagePrefab != null ? specialMessagePrefab : messagePrefab);
        if (prefab == null)
        {
            Debug.LogError($"[AssistantChatManager] Neither specialMessagePrefab nor messagePrefab is assigned.");
            return;
        }

        var msg = Instantiate(prefab, messageList);
        msg.name = $"AssistantSpecialMessage_{Time.frameCount}";
        msg.SetStretchOrigin(assistantStretchOrigin);

        header = string.IsNullOrWhiteSpace(header) ? "Note" : header.Trim();
        body = (body ?? string.Empty).Trim();
        string combined = string.IsNullOrEmpty(body) ? header : $"{header}\n\n{body}";

        msg.SetStreamOptions(streamAlmostAssistantMessage);
        if (streamAlmostAssistantMessage) msg.SetText(combined);
        else msg.SetTextImmediate(combined);

        msg.transform.SetAsLastSibling();

        onMessageTextSet?.Invoke();
    }

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

    private async void HandleUserMessage(string userMessage)
    {
        EnsureFullscreenAndExpanded();
        await HandleUserMessageCore(userMessage ?? string.Empty, userMessage ?? string.Empty, false, false);
    }

    private async System.Threading.Tasks.Task HandleUserMessageCore(
        string visText,
        string sendText,
        bool specialMessageRequested,
        bool userStreamAnimation)
    {
        EnsureFullscreenAndExpanded();

        if ((messagePrefab == null && specialMessagePrefab == null) || messageList == null)
        {
            Debug.LogError("[AssistantChatManager] messagePrefab/specialMessagePrefab or messageList is not assigned.");
            return;
        }

        if (assistant == null)
        {
            assistant = SmartAssistant.FindByTagOrNull();

            var userPrefabToUse = ResolveUserPrefab(specialMessageRequested);
            var userMsgFallback = Instantiate(userPrefabToUse, messageList);
            userMsgFallback.name = $"UserMessage_{Time.frameCount}";
            userMsgFallback.SetStretchOrigin(userStretchOrigin);
            userMsgFallback.SetStreamOptions(userStreamAnimation);
            if (userStreamAnimation) userMsgFallback.SetText(visText ?? string.Empty);
            else userMsgFallback.SetTextImmediate(visText ?? string.Empty);
            userMsgFallback.transform.SetAsLastSibling();

            var assistantPrefabToUse = ResolveAssistantPrefab();
            var assistantMsgFallback = Instantiate(assistantPrefabToUse, messageList);
            assistantMsgFallback.name = $"AssistantMessage_{Time.frameCount}";
            assistantMsgFallback.SetStretchOrigin(assistantStretchOrigin);
            ApplyText(assistantMsgFallback, "Assistant saknas i scenen (tag 'SmartAssistant').");
            assistantMsgFallback.transform.SetAsLastSibling();

            Debug.LogError("[AssistantChatManager] Could not find SmartAssistant (tag 'SmartAssistant').");
            return;
        }

        assistant.RefreshSystemPreamble(communicationSettings);

        var userPrefab = ResolveUserPrefab(specialMessageRequested);
        var userMsg = Instantiate(userPrefab, messageList);
        userMsg.name = $"UserMessage_{Time.frameCount}";
        userMsg.SetStretchOrigin(userStretchOrigin);
        userMsg.SetStreamOptions(userStreamAnimation);
        if (userStreamAnimation) userMsg.SetText(visText ?? string.Empty);
        else userMsg.SetTextImmediate(visText ?? string.Empty);
        userMsg.transform.SetAsLastSibling();

        var assistantPrefab = ResolveAssistantPrefab();
        var assistantMsg = Instantiate(assistantPrefab, messageList);
        assistantMsg.name = $"AssistantMessage_{Time.frameCount}";
        assistantMsg.SetStretchOrigin(assistantStretchOrigin);
        ApplyText(assistantMsg, string.IsNullOrEmpty(assistantThinkingText) ? "" : assistantThinkingText);
        assistantMsg.transform.SetAsLastSibling();

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

                        _ = System.Threading.Tasks.Task.Run(async () =>
                        {
                            try
                            {
                                int ms = Mathf.Max(250, Mathf.RoundToInt(firstResponseTimeoutSeconds * 1000f));
                                await System.Threading.Tasks.Task.Delay(ms).ConfigureAwait(false);
                                if (!beganStreaming && !attemptCts.IsCancellationRequested)
                                    attemptCts.Cancel();
                            }
                            catch { }
                        });

                        string finalAnswer = await assistant.SendMessageStreamToCallbackAsync(
                            sendText,
                            onPartialText: (partial) =>
                            {
                                beganStreaming = true;
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
                        int ms = Mathf.Max(500, Mathf.RoundToInt(firstResponseTimeoutSeconds * 2000f));
                        var answerTask = assistant.SendMessageAsync(
                            sendText,
                            model: openAIModel,
                            allowThinking: false,
                            ct: attemptCts.Token
                        );

                        var timeoutTask = System.Threading.Tasks.Task.Delay(ms);
                        var completed = await System.Threading.Tasks.Task.WhenAny(answerTask, timeoutTask);
                        if (completed == timeoutTask)
                        {
                            attemptCts.Cancel();
                            throw new OperationCanceledException("Non-streaming response timed out.");
                        }

                        string answer = await answerTask;
                        ApplyText(assistantMsg, answer ?? "");
                    }

                    success = true;
                    break;
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
        if (overrideSpecialMarking)
            return messagePrefab ?? specialMessagePrefab;

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
        return messagePrefab ?? specialMessagePrefab;
    }

    private StreamingMessage ResolveAssistantPrefab()
    {
        if (overrideSpecialMarking)
            return specialMessagePrefab != null ? specialMessagePrefab : messagePrefab;

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

    private void EnsureFullscreenAndExpanded()
    {
        if (!sceneManagementHeader) sceneManagementHeader = GameObject.FindGameObjectWithTag("SceneManagementHeader").GetComponent<SceneManagementHeader>();
        sceneManagementHeader.SetFullscreenState(true);

        if (expandMinimizeController == null) return;

        if (_deferredExpandRoutine != null) StopCoroutine(_deferredExpandRoutine);
        _deferredExpandRoutine = StartCoroutine(DeferredExpand());
    }

    private IEnumerator DeferredExpand()
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        if (expandMinimizeController != null)
            expandMinimizeController.Expand();

        _deferredExpandRoutine = null;
    }
}