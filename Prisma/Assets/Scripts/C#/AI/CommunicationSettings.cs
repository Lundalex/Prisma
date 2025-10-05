using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "CommunicationSettings", menuName = "Smart Assistant/Communication Settings")]
public class CommunicationSettings : ScriptableObject
{
    [Header("High-level instructions")]
    [TextArea(3, 8)] public string instructions;

    [Header("Runtime context")]
    [TextArea(3, 12)] public string context;

    [Header("Reference documentation")]
    [TextArea(6, 20)] public string documentation;

    /// <summary>
    /// Build one combined prompt in the same shape your example used.
    /// Falls back to <paramref name="fallbackInstructions"/> if local instructions are empty.
    /// </summary>
    public string BuildCombinedPrompt(string userPrompt, string fallbackInstructions = null)
    {
        var sb = new StringBuilder();

        var instr = string.IsNullOrWhiteSpace(instructions) ? fallbackInstructions : instructions;
        if (!string.IsNullOrWhiteSpace(instr))
            sb.Append(instr).Append("\n\n");

        if (!string.IsNullOrWhiteSpace(context))
            sb.Append("Context: ").Append(context).Append("\n\n");

        if (!string.IsNullOrWhiteSpace(documentation))
            sb.Append("Referensdokumentation:\n").Append(documentation).Append("\n\n");

        sb.Append("Fråga: ").Append(userPrompt ?? string.Empty);
        return sb.ToString();
    }
}