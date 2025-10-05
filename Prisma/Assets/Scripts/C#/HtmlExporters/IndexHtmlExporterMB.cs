using UnityEngine;

namespace HtmlExporters
{
    public class IndexHtmlExporterMB : MonoBehaviour
    {
        [Header("Generate")]
        [Tooltip("Toggle to generate code. Auto-resets to false.")]
        public bool buildNow = false;

        [Header("Canvas")]
        public int width = 960;
        public int height = 540;

        [Header("Page")]
        public string title = "Unity Web Player | Whirl - Fysiksimulering";

        // Kept for compatibility with older versions, but no longer used in CSS.
        [Tooltip("Vertical offset (px) for the loading bar container. (Not used in current template)")]
        public float loadingBarVerticalOffset = 0f;

        [Header("Startup Text")]
        [Tooltip("Show a startup helper text centered on the screen.")]
        public bool showStartupText = true;
        [TextArea(1, 3)]
        public string startupText = "Startar simulering";
        [Tooltip("Vertical offset from the center (px). Positive is down, negative is up.")]
        public float startupYOffset = 0f;
        [Tooltip("Font size (px) for the startup text.")]
        public float startupFontSize = 16f;

        [Header("Loading Bar")]
        [Tooltip("Height (px) of #unity-progress-bar-empty")]
        public float progressBarHeight = 7f;

        [Header("Paths / Hosting")]
        [Tooltip("Relative folder containing WebGL build files (e.g. Build)")]
        public string buildFolder = "Build";
        [Tooltip("StreamingAssets URL path")]
        private string streamingAssets = "StreamingAssets";
        [Tooltip("Manifest file href")]
        private string manifestPath = "manifest.webmanifest";
        [Tooltip("Register ServiceWorker.js on load")]
        private bool includeServiceWorker = true;

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
            var settings = new IndexHtmlSettings
            {
                width = this.width,
                height = this.height,
                title = this.title,
                loadingBarVerticalOffset = this.loadingBarVerticalOffset, // kept in struct for compatibility
                showStartupText = this.showStartupText,
                startupText = this.startupText,
                startupYOffset = this.startupYOffset,
                startupFontSize = this.startupFontSize,
                progressBarHeight = this.progressBarHeight,
                buildFolder = this.buildFolder,
                streamingAssets = this.streamingAssets,
                manifestPath = this.manifestPath,
                includeServiceWorker = this.includeServiceWorker
            };

            string html = IndexHtmlGenerator.Build(settings);
            HtmlExportUtil.CopyToClipboard("===== index.html (copied to clipboard) =====", html);
        }
    }
}