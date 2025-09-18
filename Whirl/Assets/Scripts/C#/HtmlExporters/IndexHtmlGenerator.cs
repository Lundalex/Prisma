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
        public float loadingBarVerticalOffset;
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

            string barOffsetStyle = Mathf.Abs(s.loadingBarVerticalOffset) > 0.0001f
                ? $" style=\"transform: translateY({s.loadingBarVerticalOffset.ToString(inv)}px)\""
                : string.Empty;

            string buildUrl = string.IsNullOrEmpty(s.buildFolder) ? "Build" : s.buildFolder;
            string streamingAssetsUrl = string.IsNullOrEmpty(s.streamingAssets) ? "StreamingAssets" : s.streamingAssets;
            string manifestHref = string.IsNullOrEmpty(s.manifestPath) ? "manifest.webmanifest" : s.manifestPath;

            string swBlock = s.includeServiceWorker
                ? @"      window.addEventListener(""load"", function () {
        if (""serviceWorker"" in navigator) {
          navigator.serviceWorker.register(""ServiceWorker.js"");
        }
      });"
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
      /* Centering */
      #unity-container {{
        display: flex;
        justify-content: center;
        align-items: center;
        margin: 0 auto;
        width: 100%;
        height: 100%;
      }}

      @media (min-aspect-ratio: 16/9) {{
        #unity-container {{ aspect-ratio: 16/9; }}
        #unity-canvas {{ aspect-ratio: 16/9; width: auto !important; height: 100% !important; }}
      }}

      @media (max-aspect-ratio: 16/9) {{
        #unity-container {{ aspect-ratio: 16/9; }}
        #unity-canvas {{ aspect-ratio: 16/9; width: 100% !important; height: auto !important; }}
      }}
    </style>
  </head>
  <body>
    <div id=""unity-container"">
      <canvas id=""unity-canvas"" width={s.width} height={s.height} tabindex=""-1""></canvas>
      <div id=""unity-loading-bar""{barOffsetStyle}>
        <!-- unity-logo removed -->
        <div id=""unity-progress-bar-empty"">
          <div id=""unity-progress-bar-full""></div>
        </div>
      </div>
      <div id=""unity-warning""> </div>
    </div>

    <script>
{swBlock}

      var container = document.querySelector(""#unity-container"");
      var canvas = document.querySelector(""#unity-canvas"");
      var loadingBar = document.querySelector(""#unity-loading-bar"");
      var progressBarFull = document.querySelector(""#unity-progress-bar-full"");
      var warningBanner = document.querySelector(""#unity-warning"");

      function unityShowBanner(msg, type) {{
        function updateBannerVisibility() {{
          warningBanner.style.display = warningBanner.children.length ? 'block' : 'none';
        }}
        var div = document.createElement('div');
        div.innerHTML = msg;
        warningBanner.appendChild(div);
        if (type == 'error') div.style = 'background: red; padding: 10px;';
        else {{
          if (type == 'warning') div.style = 'background: yellow; padding: 10px;';
          setTimeout(function() {{
            warningBanner.removeChild(div);
            updateBannerVisibility();
          }}, 5000);
        }}
        updateBannerVisibility();
      }}

      var buildUrl = 'Build';
      var loaderUrl = buildUrl + ""/{buildUrl}.loader.js"";
      var config = {{
        arguments: [],
        dataUrl: buildUrl + ""/{buildUrl}.data"",
        frameworkUrl: buildUrl + ""/{buildUrl}.framework.js"",
        codeUrl: buildUrl + ""/{buildUrl}.wasm"",
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

      canvas.style.background = ""url('"" + buildUrl + ""/{buildUrl}.jpg') center / cover"";
      loadingBar.style.display = ""block"";

      var script = document.createElement(""script"");
      script.src = loaderUrl;
      script.onload = () => {{
        createUnityInstance(canvas, config, (progress) => {{
          progressBarFull.style.width = 100 * progress + ""%"";
        }}).then((unityInstance) => {{
          loadingBar.style.display = ""none"";
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