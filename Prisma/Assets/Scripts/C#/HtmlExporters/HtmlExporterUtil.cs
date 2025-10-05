using UnityEngine;

namespace HtmlExporters
{
    public static class HtmlExportUtil
    {
        public static void CopyToClipboard(string header, string text)
        {
            GUIUtility.systemCopyBuffer = text;
            Debug.Log($"{header}\n{text}");
        }

        /// <summary>Remove whitespace and unsafe chars, keep letters/digits/_-</summary>
        public static string SanitizeFilenameToken(string input)
        {
            if (string.IsNullOrEmpty(input)) return "BuildName";

            // manual, allocation-light sanitizing
            var sb = new System.Text.StringBuilder(input.Length);
            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (c == '_' || c == '-' || (c >= '0' && c <= '9') ||
                    (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z'))
                {
                    sb.Append(c);
                }
            }
            if (sb.Length == 0) return "BuildName";
            return sb.ToString();
        }
    }
}