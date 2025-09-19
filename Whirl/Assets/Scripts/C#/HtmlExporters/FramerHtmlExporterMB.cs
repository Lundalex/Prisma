using UnityEngine;

namespace HtmlExporters
{
    public class FramerHtmlExporterMB : MonoBehaviour
    {
        [Header("Generate")]
        [Tooltip("Toggle to generate code. Auto-resets to false.")]
        public bool buildNow = false;

        [Header("Iframe")]
        [Tooltip("Full iframe URL (e.g. itch.io embed).")]
        public string iframeSrc = "https://itch.io/embed-upload/14981544?color=333333";

        [Header("Layout")]
        public float wrapWidth = 980f;
        public float wrapHeight = 560f;
        public float crop = 20f;
        public float edgeCrop = 5f;
        public float radius = 10f;

        [Header("UI")]
        public bool allowFullscreen = true;
        public string buttonLabel = "Helskärm";
        [Tooltip("Pixels between the fullscreen button and the top edge.")]
        public float fullscreenTopOffset = 10f;

        [Header("Font")]
        public GoogleFontFamily googleFontFamily = GoogleFontFamily.Assistant;
        public int googleFontWeight = 600;

        void OnValidate()
        {
            if (!buildNow) return;
            buildNow = false;

            if (Application.isPlaying)
            {
                Debug.LogWarning("Exit play mode before using Html exporters.");
                return;
            }
            BuildAndCopy();
        }

        [ContextMenu("Build + Copy to Clipboard")]
        void ContextBuild() => BuildAndCopy();

        void BuildAndCopy()
        {
            var settings = new FramerHtmlSettings
            {
                iframeSrc = this.iframeSrc,
                wrapWidth = this.wrapWidth,
                wrapHeight = this.wrapHeight,
                crop = this.crop,
                edgeCrop = this.edgeCrop,
                radius = this.radius,
                allowFullscreen = this.allowFullscreen,
                buttonLabel = this.buttonLabel,
                googleFontFamily = this.googleFontFamily,
                googleFontWeight = this.googleFontWeight,
                fullscreenTopOffset = this.fullscreenTopOffset
            };

            string html = FramerHtmlGenerator.Build(settings);
            HtmlExportUtil.CopyToClipboard("===== FRAMER HTML (copied to clipboard) =====", html);
        }
    }
}