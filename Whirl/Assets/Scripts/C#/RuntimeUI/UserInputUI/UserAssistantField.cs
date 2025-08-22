using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[ExecuteInEditMode]
public class UserAssistantField : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;

    [Header("Outline (UI)")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject outlineObject;

    [Header("Colors")]
    [SerializeField, ColorUsage(true, true)] private Color defaultColor = Color.gray;
    [SerializeField, ColorUsage(true, true)] private Color editColor = Color.blue;

    [Header("Behavior")]
    [Tooltip("If true, messages that are empty or whitespace will still be sent.")]
    [SerializeField] private bool allowEmptyMessages = false;
    [Tooltip("Trim whitespace before sending the message.")]
    [SerializeField] private bool trimWhitespace = true;
    [Tooltip("Keep input focus after sending so the user can type again immediately.")]
    [SerializeField] private bool keepFocusAfterSend = true;

    [Header("Events")]
    [Tooltip("Invoked when SendMessage() is called. No parameters; AssistantChatManager reads lastSentMessage.")]
    public UnityEvent onSend;

    [Header("Public Data")]
    [Tooltip("Holds the text that was just sent. AssistantChatManager reads this after onSend.")]
    public string lastSentMessage = string.Empty;

    // ─────────────────────────────────────────────────────────────────────────────
    Color _outlineBaseColor;
    RectTransform _rt;
    bool _editingOverride;

    void Awake()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (outlineImage != null)
        {
            _outlineBaseColor = defaultColor;
            outlineImage.color = defaultColor;
        }
    }

    void OnDisable()
    {
        if (_rt != null) _rt.anchoredPosition = Vector2.zero;
        if (outlineImage != null) outlineImage.color = _outlineBaseColor;
        _editingOverride = false;
    }

    void Update()
    {
        if (outlineImage == null || inputField == null) return;

        bool editing = TMPInputChecker.UserIsUsingInputField(inputField);

        if (editing)
        {
            if (!_editingOverride)
            {
                if (outlineObject != null) outlineObject.SetActive(true);
            }
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

    /// <summary>
    /// Sends the current input text via onSend (no args), updates lastSentMessage, and clears the input.
    /// </summary>
    public void SendMessage()
    {
        if (inputField == null) return;

        string message = inputField.text;
        if (trimWhitespace && message != null)
            message = message.Trim();

        if (!allowEmptyMessages && string.IsNullOrEmpty(message))
            return;

        // Expose the message publicly so AssistantChatManager can read it.
        lastSentMessage = message;

        // Fire the event (no args). The manager will read lastSentMessage.
        onSend?.Invoke();

        // Clear the field without triggering extra events
        inputField.SetTextWithoutNotify(string.Empty);
        inputField.caretPosition = 0;

        // Keep focus so the user can type the next message immediately
        if (keepFocusAfterSend)
            inputField.ActivateInputField();

        // Only focus/edit colors are used—no success/fail visuals.
    }
}