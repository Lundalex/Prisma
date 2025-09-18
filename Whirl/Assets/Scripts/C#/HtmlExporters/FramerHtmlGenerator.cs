using System.Globalization;

namespace HtmlExporters
{
    public enum GoogleFontFamily
    {
        Assistant,
        Inter,
        Roboto,
        OpenSans,
        Montserrat,
        Poppins,
        Lato,
        Nunito,
        SourceSans3
    }

    [System.Serializable]
    public struct FramerHtmlSettings
    {
        public string iframeSrc;
        public float wrapWidth;
        public float wrapHeight;
        public float crop;
        public float edgeCrop;
        public float radius;
        public bool allowFullscreen;
        public string buttonLabel;
        public GoogleFontFamily googleFontFamily;
        public int googleFontWeight;
    }

    public static class FramerHtmlGenerator
    {
        public static string Build(FramerHtmlSettings s)
        {
            var inv = CultureInfo.InvariantCulture;

            string wrapWidthPx  = s.wrapWidth.ToString(inv) + "px";
            string wrapHeightPx = s.wrapHeight.ToString(inv) + "px";
            string cropPx       = s.crop.ToString(inv) + "px";
            string edgeCropPx   = s.edgeCrop.ToString(inv) + "px";
            string radiusPx     = s.radius.ToString(inv) + "px";

            string fullscreenAttrs = s.allowFullscreen
                ? "allow=\"fullscreen\" allowfullscreen webkitallowfullscreen"
                : "";

            (string cssFamily, string gfParam) = GetGoogleFont(s.googleFontFamily);

            const string kGameWrapClass = "game-wrap";
            const string kClipClass = "clip";
            const string kIframeClass = "game-iframe";
            const string kControlsClass = "controls";
            const string kFsBtnId = "fs-btn";
            const string kFsBtnClass = "fs-btn";

            string html = $@"
<div class=""{kGameWrapClass}"" style=""--wrap-width:{wrapWidthPx}; --wrap-height:{wrapHeightPx}; --crop:{cropPx}; --edge-crop:{edgeCropPx}; --radius:{radiusPx};"">
  <div class=""{kClipClass}"">
    <iframe
      src=""{s.iframeSrc}""
      {fullscreenAttrs}
      class=""{kIframeClass}"">
    </iframe>
  </div>

  <div class=""{kControlsClass}"">
    <button id=""{kFsBtnId}"" class=""{kFsBtnClass}"" aria-label=""{s.buttonLabel}"" onclick=""(function(btn){{
      var wrap = btn.closest('.{kGameWrapClass}');
      if (wrap.requestFullscreen) wrap.requestFullscreen();
      else if (wrap.webkitRequestFullscreen) wrap.webkitRequestFullscreen();
    }})(this)"">
      <svg width=""18"" height=""18"" viewBox=""0 0 24 24"" aria-hidden=""true"">
        <path d=""M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5""
              fill=""none"" stroke=""currentColor"" stroke-width=""2""
              stroke-linecap=""round"" stroke-linejoin=""round"" />
      </svg>
      {s.buttonLabel}
    </button>
  </div>
</div>

<style>
  @import url('https://fonts.googleapis.com/css2?family={gfParam}:wght@{s.googleFontWeight}&display=swap');

  .{kGameWrapClass} {{
    position: relative;
    width: var(--wrap-width);
    height: var(--wrap-height);
    overflow: hidden;
    background: transparent;
    border-radius: var(--radius);
  }}

  .{kClipClass} {{
    position: absolute;
    inset: 0;
    overflow: hidden;
    border-radius: var(--radius);
    background: transparent;
  }}

  .{kIframeClass} {{
    position: absolute;
    top: calc(-1 * var(--edge-crop));
    left: calc(-1 * var(--edge-crop));
    width: calc(100% + (var(--edge-crop) * 2));
    height: calc(100% + var(--crop) + (var(--edge-crop) * 2));
    border: 0;
  }}

  .{kControlsClass} {{
    position: absolute;
    top: 10px;
    left: 50%;
    transform: translateX(-50%);
    z-index: 2;
  }}

  .{kFsBtnClass} {{
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: 8px 14px;
    font-family: '{cssFamily}', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    font-weight: {s.googleFontWeight};
    font-size: 16px;
    line-height: 1;
    border: 0;
    border-radius: 10px;
    background: rgba(0,0,0,.65);
    color: #fff;
    cursor: pointer;
    transition: background .15s ease, transform .02s ease;
  }}
  .{kFsBtnClass}:hover {{
    background: rgba(0,0,0,.48);
  }}
  .{kFsBtnClass}:active {{
    background: rgba(0,0,0,.40);
    transform: translateY(1px);
  }}
  .{kFsBtnClass}:focus-visible {{
    outline: 2px solid rgba(255,255,255,.6);
    outline-offset: 2px;
  }}

  .{kGameWrapClass}:fullscreen,
  .{kGameWrapClass}:-webkit-full-screen {{
    width: 100vw;
    height: 100dvh;
    max-width: none;
    max-height: none;
    border-radius: 0;
    background: transparent;
  }}
  .{kGameWrapClass}:fullscreen .{kClipClass},
  .{kGameWrapClass}:-webkit-full-screen .{kClipClass} {{
    border-radius: 0;
  }}
  .{kGameWrapClass}:fullscreen .{kIframeClass},
  .{kGameWrapClass}:-webkit-full-screen .{kIframeClass} {{
    top: calc(-1 * var(--edge-crop));
    left: calc(-1 * var(--edge-crop));
    width: calc(100% + (var(--edge-crop) * 2));
    height: calc(100% + var(--crop) + (var(--edge-crop) * 2));
  }}
  .{kGameWrapClass}:fullscreen .{kControlsClass},
  .{kGameWrapClass}:-webkit-full-screen .{kControlsClass} {{
    display: none;
  }}
</style>
".Trim();

            return html;
        }

        static (string cssFamily, string gfParam) GetGoogleFont(GoogleFontFamily family)
        {
            switch (family)
            {
                case GoogleFontFamily.Assistant:   return ("Assistant", "Assistant");
                case GoogleFontFamily.Inter:       return ("Inter", "Inter");
                case GoogleFontFamily.Roboto:      return ("Roboto", "Roboto");
                case GoogleFontFamily.OpenSans:    return ("Open Sans", "Open+Sans");
                case GoogleFontFamily.Montserrat:  return ("Montserrat", "Montserrat");
                case GoogleFontFamily.Poppins:     return ("Poppins", "Poppins");
                case GoogleFontFamily.Lato:        return ("Lato", "Lato");
                case GoogleFontFamily.Nunito:      return ("Nunito", "Nunito");
                case GoogleFontFamily.SourceSans3: return ("Source Sans 3", "Source+Sans+3");
                default:                           return ("Assistant", "Assistant");
            }
        }
    }
}
