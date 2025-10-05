using UnityEngine;
using TMPro;
using System.Text;
using System.Globalization;

[ExecuteAlways]
[DisallowMultipleComponent]
public class TMPBulletListFormatter : MonoBehaviour
{
    [Header("Input (start bullets with \"- \", one per line)")]
    [TextArea(6, 40)]
    [SerializeField] private string sourceText = @"Din text här...";
#if UNITY_EDITOR
    [Header("Target")]
    [SerializeField] TMP_Text target; // TextMeshProUGUI or TextMeshPro

    [Header("Horizontal padding (em)")]
    [Min(0)][SerializeField] float leftPaddingEm = 1.2f;  // distance from left edge to the dot
    [Min(0)][SerializeField] float bulletGapEm = 0.6f;    // gap between dot and text

    [Header("Vertical padding around bullet blocks (em)")]
    [Min(0.1f)][SerializeField] float verticalPaddingEm = 0.3f;

    // Subscript formatting
    const float subscriptSizeFactor = 1.5f;
    const float subscriptOffsetEm = -0.15f;
    const bool lockLineHeightForSubscripts = true;

    const string ResetToken = "//";
    const string ResetRichTag = "<indent=0><line-indent=0>"; // hard reset for both hanging & first-line indent

    void Reset()
    {
        target = GetComponent<TMP_Text>();
        Apply();
    }

    void OnEnable()   => Apply();
    void OnValidate() => Apply();

    public void ApplyText(string text)
    {
        sourceText = text ?? string.Empty;
        Apply();
    }

    public void Apply()
    {
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;

        string txt = (sourceText ?? string.Empty).Replace("\r\n", "\n");
        string[] lines = txt.Split('\n');

        var sb = new StringBuilder(lines.Length * 32);

        string leftEm = ToEm(leftPaddingEm);
        string indentEm = ToEm(leftPaddingEm + bulletGapEm);
        string padPct = ToPct(verticalPaddingEm);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            bool isBullet = line.StartsWith("- ");

            if (isBullet)
            {
                bool prevIsBullet = i > 0 && lines[i - 1].StartsWith("- ");

                // Add top padding only at the START of a bullet block
                if (!prevIsBullet && verticalPaddingEm > 0f)
                    sb.Append("<line-height=").Append(padPct).Append(">\n</line-height>");

                // Prepare content of the bullet
                string rawContent = line.Substring(2); // trim "- "
                bool lineHasSub = rawContent.Contains("<sub>");
                string content = EnhanceSubscripts(ApplyResetToken(rawContent));

                // Optionally lock line-height for the WHOLE LINE if it contains a subscript
                if (lockLineHeightForSubscripts && lineHasSub) sb.Append("<line-height=100%>");

                // Build one bullet line
                sb.Append(ResetRichTag) // start from a clean state per line
                  .Append("<line-indent=").Append(leftEm).Append(">•")
                  .Append("<indent=").Append(indentEm).Append(">")
                  .Append(content)
                  .Append('\n')
                  .Append(ResetRichTag); // reset so text after list is not indented

                if (lockLineHeightForSubscripts && lineHasSub) sb.Append("</line-height>");

                if (verticalPaddingEm > 0f)
                    sb.Append("<line-height=").Append(padPct).Append(">\n</line-height>");
            }
            else
            {
                // Normal line
                bool lineHasSub = line.Contains("<sub>");
                string content = EnhanceSubscripts(ApplyResetToken(line));

                if (lockLineHeightForSubscripts && lineHasSub) sb.Append("<line-height=100%>");
                sb.Append(content);
                if (lockLineHeightForSubscripts && lineHasSub) sb.Append("</line-height>");

                if (i < lines.Length - 1) sb.Append('\n');
            }
        }

        target.text = sb.ToString();
    }

    // --- Helpers -------------------------------------------------------------

    static string ApplyResetToken(string s)
    {
        // Push subsequent text back to the left margin
        // (clears hanging indent and first-line indent from this point onward)
        return s.Replace(ResetToken, ResetRichTag);
    }

    // Make every <sub>...</sub> larger and optionally shift horizontally.
    // (Do NOT alter line-height here; we wrap the entire line instead.)
    string EnhanceSubscripts(string s)
    {
        string sizeOpen  = subscriptSizeFactor != 1f ? "<size=" + ToPctFactor(subscriptSizeFactor) + ">" : string.Empty;
        string sizeClose = subscriptSizeFactor != 1f ? "</size>" : string.Empty;

        string spaceTag = Mathf.Abs(subscriptOffsetEm) > 1e-4f ? "<space=" + ToEm(subscriptOffsetEm) + ">" : string.Empty;

        // Order: optional space, optional size, then <sub>.
        // Closing: </sub> then optional </size>.
        s = s.Replace("<sub>", spaceTag + sizeOpen + "<sub>");
        s = s.Replace("</sub>", "</sub>" + sizeClose);

        return s;
    }

    static string ToEm(float v)  => v.ToString("0.###", CultureInfo.InvariantCulture) + "em";
    static string ToPct(float v) => (v * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
    static string ToPctFactor(float factor) => (factor * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
#endif
}