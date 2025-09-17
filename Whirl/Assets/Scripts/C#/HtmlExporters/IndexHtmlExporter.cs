// IndexHtmlExporter.cs
#if UNITY_EDITOR
using UnityEngine;
using System.Globalization;
using System.Text.RegularExpressions;
using UnityEditor.SceneManagement;

public class IndexHtmlExporter : HtmlExporter
{
    [Tooltip("Toggle to generate code. Auto-resets to false.")]
    public bool buildNow = false;

    [Tooltip("Canvas width attribute (pixels).")]
    public int width = 960;

    [Tooltip("Canvas height attribute (pixels).")]
    public int height = 540;

    [Tooltip("HTML <title> text.")]
    public string title = "Unity Web Player | Whirl - Fysiksimulering";

    [Tooltip("Vertical offset (px) for the loading bar container.")]
    public float loadingBarVerticalOffset = 0f;

    void OnValidate()
    {
        if (!buildNow) return;
        buildNow = false;
        if (Application.isPlaying) return;

        BuildAndCopy("===== index.html (copied to clipboard) =====");
    }

    [ContextMenu("Build + Copy to Clipboard")]
    void ContextBuild()
    {
        BuildAndCopy("===== index.html (copied to clipboard) =====");
    }

    protected override string BuildHtml()
    {
        var inv = CultureInfo.InvariantCulture;

        // Use active scene name everywhere the old hardcoded name was used.
        string sceneName = EditorSceneManager.GetActiveScene().name;
        sceneName = Sanitize(sceneName);

        // Optional vertical offset for the loading bar via inline style.
        string barOffsetStyle = Mathf.Abs(loadingBarVerticalOffset) > 0.0001f
            ? $" style=\"transform: translateY({loadingBarVerticalOffset.ToString(inv)}px)\""
            : string.Empty;

        string html = $@"<!DOCTYPE html>
<html lang=""en-us"">
  <head>
    <meta charset=""utf-8"">
    <meta http-equiv=""Content-Type"" content=""text/html; charset=utf-8"">
    <title>{title}</title>
    <link rel=""shortcut icon"" href=""TemplateData/favicon.ico"">
    <link rel=""stylesheet"" href=""TemplateData/style.css"">
    <link rel=""manifest"" href=""manifest.webmanifest"">

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

      /* When height is smaller (wider screen) */
      @media (min-aspect-ratio: 16/9) {{
        #unity-container {{
          aspect-ratio: 16/9;
        }}
        #unity-canvas {{
          aspect-ratio: 16/9;
          width: auto !important;
          height: 100% !important;
        }}
      }}

      /* When width is smaller (taller screen) */
      @media (max-aspect-ratio: 16/9) {{
        #unity-container {{
          aspect-ratio: 16/9;
        }}
        #unity-canvas {{
          aspect-ratio: 16/9;
          width: 100% !important;
          height: auto !important;
        }}
      }}
    </style>
  </head>
  <body>
    <div id=""unity-container"">
      <canvas id=""unity-canvas"" width={width} height={height} tabindex=""-1""></canvas>
      <div id=""unity-loading-bar""{barOffsetStyle}>
        <!-- unity-logo removed -->
        <div id=""unity-progress-bar-empty"">
          <div id=""unity-progress-bar-full""></div>
        </div>
      </div>
      <div id=""unity-warning""> </div>
    </div>

    <script>
      window.addEventListener(""load"", function () {{
        if (""serviceWorker"" in navigator) {{
          navigator.serviceWorker.register(""ServiceWorker.js"");
        }}
      }});

      var container = document.querySelector(""#unity-container"");
      var canvas = document.querySelector(""#unity-canvas"");
      var loadingBar = document.querySelector(""#unity-loading-bar"");
      var progressBarFull = document.querySelector(""#unity-progress-bar-full"");
      var warningBanner = document.querySelector(""#unity-warning"");

      // Shows a temporary message banner/ribbon for a few seconds, or
      // a permanent error message on top of the canvas if type=='error'.
      // If type=='warning', a yellow highlight color is used.
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

      var buildUrl = ""Build"";
      var loaderUrl = buildUrl + ""/{sceneName}.loader.js"";
      var config = {{
        arguments: [],
        dataUrl: buildUrl + ""/{sceneName}.data.br"",
        frameworkUrl: buildUrl + ""/{sceneName}.framework.js.br"",
        codeUrl: buildUrl + ""/{sceneName}.wasm.br"",
        streamingAssetsUrl: ""StreamingAssets"",
        companyName: ""DefaultCompany"",
        productName: ""{title}"",
        productVersion: ""1.0"",
        showBanner: unityShowBanner,
      }};

      if (/iPhone|iPad|iPod|Android/i.test(navigator.userAgent)) {{
        var meta = document.createElement('meta');
        meta.name = 'viewport';
        meta.content = 'width=device-width, height=device-height, initial-scale=1.0, user-scalable=no, shrink-to-fit=yes';
        document.getElementsByTagName('head')[0].appendChild(meta);
      }}

      canvas.style.background = ""url('"" + buildUrl + ""/{sceneName}.jpg') center / cover"";
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

    static string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input)) return "BuildName";
        // remove whitespace and non safe chars, keep letters/digits/_-
        string s = Regex.Replace(input, @"\s+", "");
        s = Regex.Replace(s, @"[^A-Za-z0-9_\-]", "");
        return string.IsNullOrEmpty(s) ? "BuildName" : s;
    }
}
#endif