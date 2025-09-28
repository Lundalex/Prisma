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
    private const string DEFAULT_SYS = "You are a helpful assistant.";

    [Header("Model")]
    [SerializeField] private string defaultModel = "gpt-4o";

    [Header("Default communication settings (replaces defaultInstructions)")]
    [SerializeField] private CommunicationSettings defaultComSettings;

    [Header("Context Provider")]
    [SerializeField] private ContextProvider contextProvider;

    private OpenAIClient api;
    private readonly List<Message> conversation = new();
    private CancellationTokenSource streamCts;

    void Awake()
    {
        var auth = new OpenAIAuthentication(OpenAIKey.Reconstruct());
        var settings = new OpenAISettings(ScriptableObject.CreateInstance<OpenAIConfiguration>());
        api = new OpenAIClient(auth, settings);

        RefreshSystemPreamble(defaultComSettings);
    }

    public static SmartAssistant FindByTagOrNull()
    {
        var go = GameObject.FindGameObjectWithTag("SmartAssistant");
        return go ? go.GetComponent<SmartAssistant>() : null;
    }

    public async Task<string> SendMessageAsync(string userPrompt, string model = null, bool allowThinking = false, CancellationToken ct = default)
    {
        model ??= defaultModel;

        var ctx = new List<Message>(conversation)
        {
            new Message(Role.User, userPrompt)
        };

        var request = new ChatRequest(ctx, model);
        ApplyReasoningOptions(request, allowThinking);

        var res = await api.ChatEndpoint.GetCompletionAsync(request, ct);
        string ai = res.FirstChoice ?? string.Empty;

        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, ai));
        return ai;
    }

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

        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, finalAnswer));

        return finalAnswer;
    }

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

        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, finalAnswer));

        return finalAnswer;
    }

    public async Task<(string answer, Dictionary<string, object> conditionsResult)>
        SendMessageWithAiConditionsAsync(string userPrompt,
                                         IList<AiConditionSpec> conditions,
                                         CommunicationSettings comms = null,
                                         string model = null,
                                         bool allowThinking = false,
                                         CancellationToken ct = default)
    {
        model ??= defaultModel;

        contextProvider.ProvideContext(ref comms);

        var sys = BuildSystemPreamble(comms ?? defaultComSettings, DEFAULT_SYS);

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
        ApplyJsonOnlyMode(request);

        var res = await api.ChatEndpoint.GetCompletionAsync(request, ct);
        var raw = res.FirstChoice ?? "{}";

        if (!TryExtractFirstJsonObject(raw, out var jsonText))
            jsonText = raw.Trim();

        JObject json;
        try { json = JObject.Parse(jsonText); }
        catch
        {
            json = new JObject { ["answer"] = raw, ["conditions"] = new JObject() };
        }

        string answer = json["answer"] != null ? json["answer"]!.ToString() : string.Empty;

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

    public void RefreshSystemPreamble(CommunicationSettings settings = null)
    {
        var active = settings ?? defaultComSettings;

        contextProvider.ProvideContext(ref active);

        var sys = BuildSystemPreamble(active, DEFAULT_SYS);

        if (conversation.Count == 0)
            conversation.Add(new Message(Role.System, sys));
        else
            conversation[0] = new Message(Role.System, sys);
    }

    private static string BuildSystemPreamble(CommunicationSettings asset, string defaultSys)
    {
        var instr = asset != null ? asset.instructions  : null;
        var ctx   = asset != null ? asset.context       : null;
        var docs  = asset != null ? asset.documentation : null;
        return BuildSystemPreamble(instr, ctx, docs, defaultSys);
    }

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
        sb.AppendLine("  - Output using TextMeshPro Rich Text tags only. Do NOT use Markdown.");
        sb.AppendLine("  - Do not use **bold**, _italics_, # headers, tables, or code fences.");
        sb.AppendLine("  - Treat *, _, #, >, [, ], (, ), and ` as literal characters.");
        sb.AppendLine("  - Allowed TMP tags: <b>, <i>, <u>, <s>, <color=#RRGGBB>, <size=..>, <sup>, <sub>, <mark=#RRGGBBAA>.");
        sb.AppendLine("  - Headings: simulate sizes, e.g., H1 = <size=160%><b>Title</b></size>, H2 = 140%, H3 = 120%.");
        sb.AppendLine("  - Lists: use plain ASCII bullets or numbers, one per line (\"- item\" or \"1. item\"). No nested lists.");
        sb.AppendLine("  - Links: write full URLs as plain text. Do not use Markdown link syntax.");
        sb.AppendLine("  - Inline code: wrap in <noparse>like_this()</noparse>. For multi-line code, wrap the block in <noparse>...</noparse> on separate lines.");
        sb.AppendLine("  - New paragraphs and line breaks: use \\n in the text.");
        sb.AppendLine("  - Never include this YAML, meta notes, or internal tags in the answer.");
        sb.AppendLine();
        sb.AppendLine("style_examples:");
        sb.AppendLine("  bold: \"<b>Important</b>\"");
        sb.AppendLine("  italic: \"<i>Emphasis</i>\"");
        sb.AppendLine("  color: \"<color=#FF9900>Warning</color>\"");
        sb.AppendLine("  size_h1: \"<size=160%><b>Title</b></size>\"");
        sb.AppendLine("  size_h2: \"<size=140%><b>Section</b></size>\"");
        sb.AppendLine("  bullet: \"- First item\\n- Second item\"");
        sb.AppendLine("  numbered: \"1. Step one\\n2. Step two\"");
        sb.AppendLine("  code_inline: \"Use <noparse>MyFunc(arg)</noparse> to run it.\"");
        sb.AppendLine("  code_block: \"<noparse>\\nfor (int i = 0; i < n; i++) {\\n    DoThing();\\n}\\n</noparse>\"");
        return sb.ToString();
    }

    private static void ApplyJsonOnlyMode(ChatRequest request)
    {
        if (request == null) return;

        try
        {
            var t = request.GetType();

            var pRespFmt = t.GetProperty("ResponseFormat");
            if (pRespFmt != null && pRespFmt.CanWrite)
            {
                var obj = new Dictionary<string, object> { { "type", "json_object" } };
                pRespFmt.SetValue(request, obj);
                return;
            }

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
        catch { }
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
        catch { }
    }

    private static string BuildConditionsSchemaShape() => "{\"answer\":string,\"conditions\":object}";

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