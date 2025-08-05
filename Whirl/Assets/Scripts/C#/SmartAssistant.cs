using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using OpenAI;
using OpenAI.Chat;
using TMPro;
using Newtonsoft.Json.Linq;

public class SmartAssistant : MonoBehaviour
{
    // Example usage
    async void Start()
    {
        var docs = "The fluids are simulated using SPH method, in 60 fps, real time. Rigid body collisions are solved in real time as well";
        var instructions = "You are a helpful assistant. Explain in easy-to-understand terms.";
        var conditions = new Dictionary<string, (string code, string type)>
        {
            { "The amount of words in the users question", ("wordCount", "number") },
            { "Response length exceeds 30 words", ("longAnswer", "bool") }
        };
        var userPrompt = "Explain how the fluids are simulated";

        var (answer, flags) = await SendMessageAdvancedAsync(userPrompt, instructions, docs, conditions);

        outputText.text = answer + "\n\nFlags:\n" +
                          $"Word count   : {flags["wordCount"]}\n" +
                          $"Long answer  : {flags["longAnswer"]}";
    }


    [SerializeField] TMP_Text outputText;

    private OpenAIClient api;
    private readonly List<Message> conversation = new();

    private const string apiKey = "API_KEY_HERE";

    void Awake()
    {
        var auth     = new OpenAIAuthentication(apiKey);
        var settings = new OpenAISettings(ScriptableObject.CreateInstance<OpenAIConfiguration>());

        api = new OpenAIClient(auth, settings);
        conversation.Add(new Message(Role.System, "You are a helpful assistant."));
    }

    public async Task<string> SendMessageAsync(string userPrompt, string model = "gpt-4o")
    {
        conversation.Add(new Message(Role.User, userPrompt));
        var req = new ChatRequest(conversation, model);
        var res = await api.ChatEndpoint.GetCompletionAsync(req);
        string ai = res.FirstChoice;
        conversation.Add(new Message(Role.Assistant, ai));
        return ai;
    }

    public async Task<(string answer, Dictionary<string, object> conditionsResult)>
        SendMessageAdvancedAsync(string userPrompt, string responseInstructions, string documentation, Dictionary<string,(string code,string type)> conditions, string model = "gpt-4o-mini")
    {
        var ctx = new List<Message>(conversation)
        {
            new(Role.System, responseInstructions),
            new(Role.System, "Reference documentation:\n" + documentation),
            new(Role.User , userPrompt)
        };

        var schema = new System.Text.StringBuilder();
        schema.Append("{\"answer\":string");
        foreach (var kv in conditions) schema.Append($",\"{kv.Key}\":{kv.Value.type}");
        schema.Append("}");

        ctx.Insert(0, new Message(Role.System,
            "Return ONLY valid minified JSON matching this shape: " + schema));

        var res  = await api.ChatEndpoint.GetCompletionAsync(new ChatRequest(ctx, model));
        var json = JObject.Parse(res.FirstChoice);

        string answer = json["answer"].ToString();
        var condOut   = new Dictionary<string, object>();
        foreach (var kv in conditions)
        {
            var val = json[kv.Key];
            condOut[kv.Value.code] = kv.Value.type.ToLower() switch
            {
                "bool"   => val.Value<bool>(),
                "number" => val.Value<double>(),
                _         => val.ToString()
            };
        }

        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, answer));

        return (answer, condOut);
    }
}