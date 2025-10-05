using UnityEngine;
using System.Globalization;

namespace HtmlExporters
{
    [System.Serializable]
    public struct IndexHtmlSettings
    {
        public int width;
        public int height;
        public string title;

        // Kept for compatibility, not used by the current CSS (transform now uses translateX only)
        public float loadingBarVerticalOffset;

        // Startup text on index.html
        public bool showStartupText;
        public string startupText;
        public float startupYOffset;   // px from center
        public float startupFontSize;  // px

        // NEW: height for #unity-progress-bar-empty
        public float progressBarHeight; // px

        public string buildFolder;
        public string streamingAssets;    // e.g. "StreamingAssets"
        public string manifestPath;       // e.g. "manifest.webmanifest"
        public bool includeServiceWorker; // register ServiceWorker.js
    }

    public static class IndexHtmlGenerator
    {
        public static string Build(IndexHtmlSettings s)
        {
            var inv = CultureInfo.InvariantCulture;

            string buildFolder = s.buildFolder;
            string streamingAssetsUrl = string.IsNullOrEmpty(s.streamingAssets) ? "StreamingAssets" : s.streamingAssets;
            string manifestHref = string.IsNullOrEmpty(s.manifestPath) ? "manifest.webmanifest" : s.manifestPath;

            string swBlock = s.includeServiceWorker
                ? @"      window.addEventListener(""load"", function () {
        if (""serviceWorker"" in navigator) {
          navigator.serviceWorker.register(""ServiceWorker.js"");
        }
      });"
                : "";

            // Inline CSS variables (no bar-y-offset anymore)
            string containerVars =
                $" style=\"--st-y-offset:{(-s.startupYOffset).ToString(inv)}px; --st-font-size:{s.startupFontSize.ToString(inv)}px;\"";

            string startupHtml = (s.showStartupText && !string.IsNullOrEmpty(s.startupText))
                ? $@"      <div id=""startup-text"" class=""startup-text"">{s.startupText}</div>"
                : "";

            string html = $@"<!DOCTYPE html>
<html lang=""en-us"">
  <head>
    <meta charset=""utf-8"">
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
    <title>{s.title}</title>
    <link rel=""shortcut icon"" href=""TemplateData/favicon.ico"">
    <link rel=""stylesheet"" href=""TemplateData/style.css"">
    <link rel=""manifest"" href=""{manifestHref}"">

    <style>
      /* Centering the playable area */
      html, body {{
        height: 100%;
      }}
      body {{
        margin: 0;
        background: transparent;
        color: #fff;
      }}
      #unity-container {{
        display: flex;
        justify-content: center;
        align-items: center;
        margin: 0 auto;
        width: 100%;
        height: 100%;
        position: relative;
        background: transparent !important;
      }}

      /* Aspect ratio helpers for responsiveness */
      @media (min-aspect-ratio: 16/9) {{
        #unity-container {{ aspect-ratio: 16/9; }}
        #unity-canvas {{ aspect-ratio: 16/9; width: auto !important; height: 100% !important; }}
      }}
      @media (max-aspect-ratio: 16/9) {{
        #unity-container {{ aspect-ratio: 16/9; }}
        #unity-canvas {{ aspect-ratio: 16/9; width: 100% !important; height: auto !important; }}
      }}

      /* Loading bar */
      #unity-loading-bar {{
        position: absolute;
        left: 50%;
        bottom: 32px;
        transform: translateX(-50%);
        width: min(520px, 80vw);
        padding: 0;
        display: none;
      }}
      #unity-progress-bar-empty {{
        position: relative;
        height: {s.progressBarHeight.ToString(inv)}px;
        width: 100%;
        border-radius: 999px;
        background: rgba(255,255,255,0.18);
        border: 1px solid rgba(255,255,255,0.35);
        overflow: hidden;
      }}

      /* Fill: vertically centered inside the track (intentionally tall per request) */
      #unity-progress-bar-full {{
        position: absolute;
        left: 0;
        top: 50%;
        transform: translateY(-50%);
        height: 1000px;
        width: 0%;
        border-radius: 999px;
        background: linear-gradient(90deg, #B050AA, #538CD4);
        transition: width .15s ease-out;
      }}

      /* Startup helper text (centered, offset relative to center) */
      .startup-text {{
        position: absolute;
        top: 50%;
        left: 50%;
        transform: translate(-50%, -50%) translateY(var(--st-y-offset));
        z-index: 3;
        pointer-events: none;
        font-family: system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
        font-size: var(--st-font-size);
        font-weight: 600;
        color: rgba(255,255,255,0.95);
        text-shadow: 0 2px 8px rgba(0,0,0,0.6);
        white-space: pre-wrap;
        text-align: center;
        padding: 0 12px;
        user-select: none;
      }}
    </style>
  </head>
  <body>
    <div id=""unity-container""{containerVars}>
      <canvas id=""unity-canvas"" width={s.width} height={s.height} tabindex=""-1""></canvas>

      <!-- Loading bar -->
      <div id=""unity-loading-bar"">
        <div id=""unity-progress-bar-empty"">
          <div id=""unity-progress-bar-full""></div>
        </div>
      </div>

{startupHtml}
      <div id=""unity-warning""></div>
    </div>

    <script>
{swBlock}

      var container = document.querySelector(""#unity-container"");
      var canvas = document.querySelector(""#unity-canvas"");
      var loadingBar = document.querySelector(""#unity-loading-bar"");
      var progressBarFull = document.querySelector(""#unity-progress-bar-full"");
      var warningBanner = document.querySelector(""#unity-warning"");
      var startupTextEl = document.querySelector(""#startup-text"");

      function unityShowBanner(msg, type) {{
        function updateBannerVisibility() {{
          warningBanner.style.display = warningBanner.children.length ? 'block' : 'none';
        }}
        var div = document.createElement('div');
        div.innerHTML = msg;
        warningBanner.appendChild(div);
        if (type === 'error') div.style = 'background: red; padding: 10px;';
        else {{
          if (type === 'warning') div.style = 'background: yellow; padding: 10px;';
          setTimeout(function() {{
            warningBanner.removeChild(div);
            updateBannerVisibility();
          }}, 5000);
        }}
        updateBannerVisibility();
      }}

      var buildUrl = 'Build';
      var loaderUrl = buildUrl + ""/{buildFolder}.loader.js"";
      var config = {{
        arguments: [],
        dataUrl: buildUrl + ""/{buildFolder}.data"",
        frameworkUrl: buildUrl + ""/{buildFolder}.framework.js"",
        codeUrl: buildUrl + ""/{buildFolder}.wasm"",
        streamingAssetsUrl: ""{streamingAssetsUrl}"",
        companyName: ""DefaultCompany"",
        productName: ""{s.title}"",
        productVersion: ""1.0"",
        showBanner: unityShowBanner
      }};

      if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {{
        var meta = document.createElement('meta');
        meta.name = 'viewport';
        meta.content = 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes';
        document.getElementsByTagName('head')[0].appendChild(meta);
      }}

      canvas.style.background = ""url('"" + buildUrl + ""/{buildFolder}.jpg') center / cover"";
      loadingBar.style.display = ""block"";

      var script = document.createElement(""script"");
      script.src = loaderUrl;
      script.onload = () => {{
        createUnityInstance(canvas, config, (progress) => {{
          // progress is 0..1
          progressBarFull.style.width = (progress * 100) + ""%"";
        }}).then((unityInstance) => {{
          loadingBar.style.display = ""none"";
          if (startupTextEl) startupTextEl.style.display = ""none"";
        }}).catch((message) => {{
          alert(message);
        }});
      }};
      document.body.appendChild(script);
    </script>
  </body>
</html>";
            return html.Trim();
        }
    }
}