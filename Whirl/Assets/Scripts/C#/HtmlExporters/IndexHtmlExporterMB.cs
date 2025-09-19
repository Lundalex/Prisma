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
        [Tooltip("Vertical offset (px) for the loading bar container.")]
        public float loadingBarVerticalOffset = 0f;

        [Header("Paths / Hosting")]
        [Tooltip("Relative folder containing WebGL build files (e.g. Build)")]
        public string buildFolder = "Build";
        [Tooltip("StreamingAssets URL path")]
        public string streamingAssets = "StreamingAssets";
        [Tooltip("Manifest file href")]
        public string manifestPath = "manifest.webmanifest";
        [Tooltip("Register ServiceWorker.js on load")]
        public bool includeServiceWorker = true;

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
                loadingBarVerticalOffset = this.loadingBarVerticalOffset,
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
