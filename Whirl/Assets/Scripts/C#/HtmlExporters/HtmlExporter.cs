// HtmlExporter.cs
#if UNITY_EDITOR
using UnityEngine;

public abstract class HtmlExporter : MonoBehaviour
{
    protected void BuildAndCopy(string header)
    {
        if (Application.isPlaying) return;

        string html = BuildHtml();
        GUIUtility.systemCopyBuffer = html;
        Debug.Log($"{header}\n{html}");
    }

    protected abstract string BuildHtml();
}
#endif