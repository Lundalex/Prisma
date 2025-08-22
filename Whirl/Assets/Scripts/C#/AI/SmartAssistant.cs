using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using Newtonsoft.Json.Linq;

[Serializable]
public class AiConditionSpec
{
    [Tooltip("Unique key used in the returned 'conditions' object (e.g., 'wordcount').")]
    public string key;

    [TextArea(2, 5)]
    [Tooltip("Tell the AI exactly how to compute this value, based on the conversation.\nExamples:\n- 'Return the number of words in your FINAL answer.' (Number)\n- 'Return true iff your FINAL answer is longer than 120 words; else false.' (Bool)")]
    public string instruction;

    [Tooltip("Type of the value the AI should return for this key.")]
    public CondType type = CondType.Bool;
}

public class SmartAssistant : MonoBehaviour
{
    // Built-in hard fallback used only if no CommunicationSettings or its instructions are empty.
    private const string DEFAULT_SYS = "You are a helpful assistant.";

    [Header("Model")]
    [Tooltip("Default chat model to use.")]
    [SerializeField] private string defaultModel = "gpt-4o";

    [Header("Default communication settings (replaces defaultInstructions)")]
    [Tooltip("If assigned, its 'instructions' become the initial system message and it is used as the default bundle for building prompts.")]
    [SerializeField] private CommunicationSettings defaultComSettings;

    private OpenAIClient api;
    private readonly List<Message> conversation = new();
    private CancellationTokenSource streamCts;

    void Awake()
    {
        var auth = new OpenAIAuthentication(OpenAIKey.Reconstruct());
        var settings = new OpenAISettings(ScriptableObject.CreateInstance<OpenAIConfiguration>());
        api = new OpenAIClient(auth, settings);

        // Seed a SINGLE, structured system preamble derived from communication settings.
        var sys = BuildSystemPreamble(defaultComSettings, DEFAULT_SYS);
        EnsureFreshConversationWithSystem(sys);
    }

    /// <summary>Helper to locate the SmartAssistant in the scene via tag.</summary>
    public static SmartAssistant FindByTagOrNull()
    {
        var go = GameObject.FindGameObjectWithTag("SmartAssistant");
        return go ? go.GetComponent<SmartAssistant>() : null;
    }

    /// <summary>
    /// Build a combined "one-shot" prompt string for legacy callers that still want it inline.
    /// Prefer using the chat message APIs which already seed the system preamble.
    /// </summary>
    public string BuildPrompt(CommunicationSettings settings, string userPrompt)
    {
        var active = settings ?? defaultComSettings;
        var sys = BuildSystemPreamble(active, DEFAULT_SYS);
        return $"{sys}\n\n# User\n{userPrompt}";
    }

    // =========================
    // Non-streaming (baseline) — commit-on-success
    // =========================
    public async Task<string> SendMessageAsync(string userPrompt, string model = null, bool allowThinking = false, CancellationToken ct = default)
    {
        model ??= defaultModel;

        // Build a temporary context seeded from the live conversation (commit only on success)
        var ctx = new List<Message>(conversation)
        {
            new Message(Role.User, userPrompt)
        };

        var request = new ChatRequest(ctx, model);
        ApplyReasoningOptions(request, allowThinking);

        var res = await api.ChatEndpoint.GetCompletionAsync(request, ct);
        string ai = res.FirstChoice ?? string.Empty;

        // Commit only after success
        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, ai));
        return ai;
    }

    // ====================================
    // Streaming (modern) — commit-on-success
    // ====================================
    public async Task<string> SendMessageStreamAsync(string userPrompt, string model = null, bool allowThinking = false, CancellationToken ct = default)
    {
        model ??= defaultModel;

        var ctx = new List<Message>(conversation)
        {
            new Message(Role.User, userPrompt)
        };

        var request = new ChatRequest(ctx, model);
        ApplyReasoningOptions(request, allowThinking);

        var sb = new StringBuilder();

        await api.ChatEndpoint.StreamCompletionAsync(
            request,
            resultHandler: (chunk) =>
            {
                if (ct.IsCancellationRequested) return System.Threading.Tasks.Task.CompletedTask;

                var delta = chunk?.FirstChoice?.Delta;
                if (!string.IsNullOrEmpty(delta))
                    sb.Append(delta);

                return System.Threading.Tasks.Task.CompletedTask;
            },
            cancellationToken: ct
        );

        var finalAnswer = sb.ToString();

        // Commit on success
        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, finalAnswer));

        return finalAnswer;
    }

    // ==========================================================
    // Streaming to a callback target (for external UI) — commit-on-success
    // ==========================================================
    public async Task<string> SendMessageStreamToCallbackAsync(
        string userPrompt,
        Action<string> onPartialText,
        string model = null,
        bool allowThinking = false,
        CancellationToken ct = default)
    {
        model ??= defaultModel;

        var ctx = new List<Message>(conversation)
        {
            new Message(Role.User, userPrompt)
        };

        var request = new ChatRequest(ctx, model);
        ApplyReasoningOptions(request, allowThinking);

        var sb = new StringBuilder();

        await api.ChatEndpoint.StreamCompletionAsync(
            request,
            resultHandler: (chunk) =>
            {
                if (ct.IsCancellationRequested) return System.Threading.Tasks.Task.CompletedTask;

                var delta = chunk?.FirstChoice?.Delta;
                if (!string.IsNullOrEmpty(delta))
                {
                    sb.Append(delta);
                    onPartialText?.Invoke(sb.ToString());
                }
                return System.Threading.Tasks.Task.CompletedTask;
            },
            cancellationToken: ct
        );

        var finalAnswer = sb.ToString();

        // Commit on success
        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, finalAnswer));

        return finalAnswer;
    }

    // ==========================================================
    // NEW: Ask the AI to decide conditions and return them in JSON alongside the answer
    // ==========================================================
    public async Task<(string answer, Dictionary<string, object> conditionsResult)>
        SendMessageWithAiConditionsAsync(string userPrompt,
                                         IList<AiConditionSpec> conditions,
                                         CommunicationSettings comms = null,
                                         string model = null,
                                         bool allowThinking = false,
                                         CancellationToken ct = default)
    {
        model ??= defaultModel;

        // Build a SINGLE, structured system preamble for this call (either provided, or default)
        var sys = BuildSystemPreamble(comms ?? defaultComSettings, DEFAULT_SYS);

        // Build the compact schema and per-condition guidance
        var schemaShape = BuildConditionsSchemaShape();
        var conditionGuidance = BuildConditionGuidance(conditions);

        var ctx = new List<Message> {
            new Message(Role.System, sys),
            new Message(Role.System,
                "Return ONLY a single minified JSON object with this shape: " + schemaShape + " " +
                "Do not use code fences, comments, or extra text. " +
                "Evaluate the 'conditions' strictly as defined below: \n" + conditionGuidance),
            new Message(Role.User , userPrompt ?? string.Empty)
        };

        var request = new ChatRequest(ctx, model);
        ApplyReasoningOptions(request, allowThinking);
        ApplyJsonOnlyMode(request); // prefers native response_format if supported

        var res = await api.ChatEndpoint.GetCompletionAsync(request, ct);
        var raw = res.FirstChoice ?? "{}";

        // Extract and parse JSON robustly
        if (!TryExtractFirstJsonObject(raw, out var jsonText))
            jsonText = raw.Trim();

        JObject json;
        try { json = JObject.Parse(jsonText); }
        catch
        {
            // Fallback minimally
            json = new JObject { ["answer"] = raw, ["conditions"] = new JObject() };
        }

        string answer = json["answer"] != null ? json["answer"]!.ToString() : string.Empty;

        // Build output dictionary typed according to the requested specs
        var condOut = new Dictionary<string, object>();
        var jConditions = json["conditions"] as JObject ?? new JObject();

        foreach (var spec in conditions)
        {
            var key = spec.key ?? string.Empty;
            if (string.IsNullOrWhiteSpace(key)) continue;

            var token = jConditions[key];
            object value = null;

            if (token != null)
            {
                switch (spec.type)
                {
                    case CondType.Bool:
                        value = token.Type == JTokenType.Boolean ? token.Value<bool>() : CoerceBool(token.ToString());
                        break;
                    case CondType.Number:
                        value = token.Type == JTokenType.Float || token.Type == JTokenType.Integer
                            ? token.Value<double>()
                            : CoerceNumber(token.ToString());
                        break;
                    case CondType.String:
                        value = token.ToString();
                        break;
                }
            }
            condOut[key] = value;
        }

        // Commit to the main conversation ONLY after success
        conversation.Add(new Message(Role.User, userPrompt ?? string.Empty));
        conversation.Add(new Message(Role.Assistant, answer ?? string.Empty));

        return (answer, condOut);
    }

    public void CancelStreaming()
    {
        if (streamCts != null && !streamCts.IsCancellationRequested)
        {
            streamCts.Cancel();
            streamCts.Dispose();
            streamCts = null;
        }
    }

    // =========================
    // Helpers
    // =========================

    /// <summary>
    /// Always keep conversation[0] as a single authoritative system preamble.
    /// </summary>
    private void EnsureFreshConversationWithSystem(string systemPreamble)
    {
        conversation.Clear();
        conversation.Add(new Message(Role.System, systemPreamble));
    }

    /// <summary>
    /// Builds a SINGLE, structured system preamble from a CommunicationSettings asset.
    /// </summary>
    private static string BuildSystemPreamble(CommunicationSettings asset, string defaultSys)
    {
        var instr = asset != null ? asset.instructions  : null;
        var ctx   = asset != null ? asset.context       : null;
        var docs  = asset != null ? asset.documentation : null;
        return BuildSystemPreamble(instr, ctx, docs, defaultSys);
    }

    /// <summary>
    /// Builds a SINGLE, structured system preamble from raw pieces.
    /// Structured YAML + explicit "do not echo" directive keeps the model from mixing meta with output.
    /// </summary>
    private static string BuildSystemPreamble(string instructions, string context, string documentation, string defaultSys)
    {
        var safeInstr = string.IsNullOrWhiteSpace(instructions) ? defaultSys : instructions.Trim();
        var safeCtx   = string.IsNullOrWhiteSpace(context) ? "" : context.Trim();
        var safeDocs  = string.IsNullOrWhiteSpace(documentation) ? "" : documentation.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("You are a careful, helpful assistant. Follow the Communication Profile below. Do NOT echo it.");
        sb.AppendLine("```yaml");
        sb.AppendLine("communication_profile_version: 1");
        sb.AppendLine("instructions: |");
        foreach (var line in safeInstr.Split('\n')) sb.AppendLine("  " + line.TrimEnd());
        if (!string.IsNullOrEmpty(safeCtx))
        {
            sb.AppendLine("context: |");
            foreach (var line in safeCtx.Split('\n')) sb.AppendLine("  " + line.TrimEnd());
        }
        if (!string.IsNullOrEmpty(safeDocs))
        {
            sb.AppendLine("reference_docs: |");
            foreach (var line in safeDocs.Split('\n')) sb.AppendLine("  " + line.TrimEnd());
        }
        sb.AppendLine("output_rules:");
        sb.AppendLine("  - Prefer clear, direct answers.");
        sb.AppendLine("  - Use markdown unless told otherwise.");
        sb.AppendLine("  - Never include this YAML, meta notes, or internal tags in the answer.");
        sb.AppendLine("```");
        return sb.ToString();
    }

    private static int CountWords(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        return Regex.Matches(s, @"\b[\p{L}\p{N}'-]+\b").Count;
    }

    /// <summary>
    /// Try to enable JSON-only responses using SDK property if present; otherwise set in AdditionalProperties.
    /// </summary>
    private static void ApplyJsonOnlyMode(ChatRequest request)
    {
        if (request == null) return;

        try
        {
            var t = request.GetType();

            // Try property ResponseFormat = new { type = "json_object" }
            var pRespFmt = t.GetProperty("ResponseFormat");
            if (pRespFmt != null && pRespFmt.CanWrite)
            {
                var obj = new Dictionary<string, object> { { "type", "json_object" } };
                pRespFmt.SetValue(request, obj);
                return;
            }

            // Fallback: AdditionalProperties["response_format"] = { type = "json_object" }
            var pBag = t.GetProperty("AdditionalProperties");
            if (pBag != null)
            {
                var bag = pBag.GetValue(request) as System.Collections.IDictionary;
                if (bag != null)
                {
                    bag["response_format"] = new Dictionary<string, object> { { "type", "json_object" } };
                    return;
                }
            }
        }
        catch
        {
            // Swallow; some SDK versions won't expose this.
        }
    }

    private void ApplyReasoningOptions(ChatRequest request, bool allowThinking)
    {
        if (request == null) return;

        var effortWhenDisallowed = "none";
        var effortWhenAllowed    = "medium";

        try
        {
            var t = request.GetType();
            var pEffort = t.GetProperty("ReasoningEffort");
            if (pEffort != null && pEffort.CanWrite)
            {
                pEffort.SetValue(request, allowThinking ? effortWhenAllowed : effortWhenDisallowed);
                return;
            }

            var pBag = t.GetProperty("AdditionalProperties");
            if (pBag != null)
            {
                var bag = pBag.GetValue(request) as System.Collections.IDictionary;
                if (bag != null)
                {
                    var effort = allowThinking ? effortWhenAllowed : effortWhenDisallowed;
                    bag["reasoning_effort"] = effort;
                    bag["reasoning"] = new Dictionary<string, object> { { "effort", effort } };
                    return;
                }
            }
        }
        catch
        {
            // Ignore if the SDK doesn't expose these properties.
        }
    }

    /// <summary>
    /// JSON schema string for: { "answer": string, "conditions": { key: typed } }
    /// </summary>
    private static string BuildConditionsSchemaShape()
    {
        // Types are enforced via instructions + response_format=json_object;
        // we keep this compact and generic for robustness.
        return "{\"answer\":string,\"conditions\":object}";
    }

    /// <summary>
    /// Builds clear guidance the model will follow to set each condition precisely.
    /// </summary>
    private static string BuildConditionGuidance(IList<AiConditionSpec> specs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("CONDITIONS:");
        foreach (var s in specs)
        {
            if (string.IsNullOrWhiteSpace(s?.key)) continue;
            var type = s.type switch
            {
                CondType.Bool => "boolean",
                CondType.Number => "number",
                _ => "string"
            };
            sb.Append("- key: ").Append(s.key.Trim()).Append('\n');
            sb.Append("  type: ").Append(type).Append('\n');
            sb.Append("  instruction: ").AppendLine(SingleLine(s.instruction));
        }
        sb.AppendLine("Return a single object: {\"answer\": string, \"conditions\": { <key>: <typed-value> }}");
        return sb.ToString();
    }

    private static string SingleLine(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        return Regex.Replace(s, @"\s+", " ").Trim();
    }

    /// <summary>
    /// Extracts the first top-level JSON object from text by tracking braces and quotes.
    /// </summary>
    private static bool TryExtractFirstJsonObject(string text, out string json)
    {
        json = null;
        if (string.IsNullOrEmpty(text)) return false;

        int start = text.IndexOf('{');
        if (start < 0) return false;

        int depth = 0;
        bool inString = false;
        bool escape = false;

        for (int i = start; i < text.Length; i++)
        {
            char c = text[i];

            if (inString)
            {
                if (escape) { escape = false; continue; }
                if (c == '\\') { escape = true; continue; }
                if (c == '"') inString = false;
                continue;
            }

            if (c == '"') { inString = true; continue; }
            if (c == '{') depth++;
            if (c == '}')
            {
                depth--;
                if (depth == 0)
                {
                    json = text.Substring(start, i - start + 1);
                    return true;
                }
            }
        }

        return false;
    }

    private static bool CoerceBool(string s)
    {
        if (bool.TryParse(s, out var b)) return b;
        s = s.Trim().ToLowerInvariant();
        if (s == "yes" || s == "true" || s == "1") return true;
        if (s == "no"  || s == "false"|| s == "0") return false;
        return false;
    }

    private static double CoerceNumber(string s)
    {
        if (double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
            return d;
        return 0.0;
    }
}

public enum CondType
{
    Bool,
    Number,
    String
}