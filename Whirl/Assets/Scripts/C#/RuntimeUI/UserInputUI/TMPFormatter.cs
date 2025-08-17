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
    [SerializeField] public string sourceText = @"Din text här...";

    [Header("Target")]
    [SerializeField] TMP_Text target; // TextMeshProUGUI or TextMeshPro

    [Header("Horizontal padding (em)")]
    [Min(0)] [SerializeField] float leftPaddingEm = 1.2f;  // distance from left edge to the dot
    [Min(0)] [SerializeField] float bulletGapEm   = 0.6f;  // gap between dot and text

    [Header("Vertical padding around bullet blocks (em)")]
    [Min(0.1f)] [SerializeField] float verticalPaddingEm = 0.3f;

    const string ResetToken = "//";

    void Reset()
    {
        target = GetComponent<TMP_Text>();
        Apply();
    }

    void OnEnable()   => Apply();
    void OnValidate() => Apply();

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        if (target == null) target = GetComponent<TMP_Text>();
        if (target == null) return;

        string txt = (sourceText ?? string.Empty).Replace("\r\n", "\n");
        string[] lines = txt.Split('\n');

        var sb = new StringBuilder(lines.Length * 32);

        string leftEm   = ToEm(leftPaddingEm);
        string indentEm = ToEm(leftPaddingEm + bulletGapEm);
        string padPct   = ToPct(verticalPaddingEm);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            bool isBullet = line.StartsWith("- ");

            if (isBullet)
            {
                bool prevIsBullet = i > 0 && lines[i - 1].StartsWith("- ");
                bool nextIsBullet = i + 1 < lines.Length && lines[i + 1].StartsWith("- ");

                // Add top padding only at the START of a bullet block
                if (!prevIsBullet && verticalPaddingEm > 0f)
                    sb.Append("<line-height=").Append(padPct).Append(">\n</line-height>");

                string content = line.Substring(2); // trim "- "
                content = ApplyResetToken(content);

                // Reset indent to avoid accumulation between bullets
                sb.Append("<indent=0>")
                  .Append("<line-indent=").Append(leftEm).Append(">•")
                  .Append("<indent=").Append(indentEm).Append(">")
                  .Append(content)
                  .Append('\n')
                  .Append("<indent=0>");

                if (verticalPaddingEm > 0f)
                    sb.Append("<line-height=").Append(padPct).Append(">\n</line-height>");
            }
            else
            {
                // Normal line, still honor the // reset token
                sb.Append(ApplyResetToken(line));
                if (i < lines.Length - 1) sb.Append('\n');
            }
        }

        target.text = sb.ToString();
    }

    static string ApplyResetToken(string s)
    {
        // Push subsequent text back to the left margin
        // (clears hanging indent and first-line indent from this point onward)
        return s.Replace(ResetToken, "<indent=0><line-indent=0>");
    }

    static string ToEm(float v)  => v.ToString("0.###", CultureInfo.InvariantCulture) + "em";
    static string ToPct(float v) => (v * 100f).ToString("0.##", CultureInfo.InvariantCulture) + "%";
}