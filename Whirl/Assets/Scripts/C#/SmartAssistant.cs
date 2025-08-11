using System.Collections.Generic;
using System.Text;
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
        var docs = "Vätskorna simuleras med SPH-metoden, i 60 fps, realtid. Även stelkroppskollisioner löses i realtid.";
        var instructions = "Du är en hjälpsam assistent. Förklara på ett lättförståeligt sätt. Svara på svenska om inte något annat språk efterfrågas";
        var context = "för närvarande simulerad vätska: honung";
        var conditions = new Dictionary<string, (string code, CondType type)>
        {
            { "The amount of words in the users question", ("wordCount",CondType.Number) },
            { "Response length exceeds 30 words"          , ("longAnswer",CondType.Bool) }
        };

        var userPrompt = "Förklara tydligt och detaljerat hur vätskorna simuleras. Vilken vätska ser jag för närvarande?";

        var (answer, flags) = await SendMessageAdvancedAsync(
                                userPrompt,
                                instructions,
                                context,
                                docs,
                                conditions);

        outputText.text = $"{answer}\n\nFlags:\nwordCount : {flags["wordCount"]}\nlongAnswer: {flags["longAnswer"]}";
    }

    [SerializeField] TMP_Text outputText;

    private OpenAIClient api;
    private readonly List<Message> conversation = new();

    void Awake()
    {
        // Build key in memory and use it to auth
        var auth = new OpenAIAuthentication(OpenAIKey.Reconstruct());

        var settings = new OpenAISettings(ScriptableObject.CreateInstance<OpenAIConfiguration>());
        api = new OpenAIClient(auth, settings);

        conversation.Add(new Message(Role.System, "You are a helpful assistant."));
    }

    public async Task<string> SendMessageAsync(string userPrompt, string model = "gpt-4o")
    {
        conversation.Add(new Message(Role.User, userPrompt));
        var res = await api.ChatEndpoint.GetCompletionAsync(new ChatRequest(conversation, model));
        string ai = res.FirstChoice;
        conversation.Add(new Message(Role.Assistant, ai));
        return ai;
    }

    public async Task<(string answer, Dictionary<string, object> conditionsResult)>
        SendMessageAdvancedAsync(string userPrompt,
                                 string responseInstructions,
                                 string contextInfo,
                                 string documentation,
                                 Dictionary<string, (string code, CondType type)> conditions,
                                 string model = "gpt-4o")
    {
        var ctx = new List<Message>(conversation)
        {
            new(Role.System, responseInstructions),
            new(Role.System, "Context: " + contextInfo),
            new(Role.System, "Reference documentation:\n" + documentation),
            new(Role.User ,  userPrompt)
        };

        var schema = new StringBuilder("{\"answer\":string");
        foreach (var kv in conditions) schema.Append($",\"{kv.Key}\":{kv.Value.type}");
        schema.Append('}');

        ctx.Insert(0, new Message(Role.System,
            "Return ONLY valid minified JSON matching this shape: " + schema));

        var res = await api.ChatEndpoint.GetCompletionAsync(new ChatRequest(ctx, model));
        var json = JObject.Parse(res.FirstChoice);

        string answer = json["answer"].ToString();
        var condOut = new Dictionary<string, object>();
        foreach (var kv in conditions)
        {
            var val = json[kv.Key];
            condOut[kv.Value.code] = kv.Value.type switch
            {
                CondType.Bool => val.Value<bool>(),
                CondType.Number => val.Value<double>(),
                _ => val.ToString()
            };
        }

        conversation.Add(new Message(Role.User, userPrompt));
        conversation.Add(new Message(Role.Assistant, answer));

        return (answer, condOut);
    }
}

public enum CondType
{
    Bool,
    Number,
    String
}