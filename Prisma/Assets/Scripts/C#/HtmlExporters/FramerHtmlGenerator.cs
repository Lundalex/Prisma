using System.Globalization;
using UnityEngine;

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
        public float fullscreenTopOffset;
        public float buttonFontSize;    // px
        public float iconScale;         // multiplier for SVG icon
        public float buttonPadX;        // px
        public float buttonPadY;        // px
        public float buttonCornerRadius;// px
        public Color buttonBgColor;     // HDR → CSS
        public Color buttonOutlineColor; // HDR → CSS
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
            string fsTopPx      = s.fullscreenTopOffset.ToString(inv) + "px";

            // Vars for button
            string fsFontPx     = s.buttonFontSize.ToString(inv) + "px";
            string fsIconScale  = s.iconScale.ToString(inv);
            string fsPadX       = s.buttonPadX.ToString(inv) + "px";
            string fsPadY       = s.buttonPadY.ToString(inv) + "px";
            string fsBtnRadius  = s.buttonCornerRadius.ToString(inv) + "px";

            // Colors (force fully opaque; lighten on hover/active)
            string bgBase   = CssOpaqueColorFromUnity(s.buttonBgColor);
            string bgHover  = CssLightenOpaque(s.buttonBgColor, 0.12f);  // slightly lighter, opaque
            string bgActive = CssLightenOpaque(s.buttonBgColor, 0.20f);  // a bit more, opaque
            string outline  = CssColorFromUnity(s.buttonOutlineColor);    // keep outline alpha

            string fullscreenAttrs = s.allowFullscreen
                ? "allow=\"fullscreen\" allowfullscreen webkitallowfullscreen"
                : "";

            (string cssFamily, string gfParam) = GetGoogleFont(s.googleFontFamily);

            const string kGameWrapClass = "game-wrap";
            const string kClipClass     = "clip";
            const string kIframeClass   = "game-iframe";
            const string kControlsClass = "controls";
            const string kFsBtnId       = "fs-btn";
            const string kFsBtnClass    = "fs-btn";

            string html = $@"
<div class=""{kGameWrapClass}"" style=""--wrap-width:{wrapWidthPx}; --wrap-height:{wrapHeightPx}; --crop:{cropPx}; --edge-crop:{edgeCropPx}; --radius:{radiusPx}; --fs-top-offset:{fsTopPx};
  --fs-font-size:{fsFontPx}; --fs-icon-scale:{fsIconScale}; --fs-pad-x:{fsPadX}; --fs-pad-y:{fsPadY}; --fs-radius-btn:{fsBtnRadius};
  --fs-bg:{bgBase}; --fs-bg-hover:{bgHover}; --fs-bg-active:{bgActive}; --fs-outline:{outline};"">
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
    top: var(--fs-top-offset);
    left: 50%;
    transform: translateX(-50%);
    z-index: 2;
  }}

  .{kFsBtnClass} {{
    display: inline-flex;
    align-items: center;
    gap: 8px;
    padding: var(--fs-pad-y) var(--fs-pad-x);
    font-family: '{cssFamily}', system-ui, -apple-system, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
    font-weight: {s.googleFontWeight};
    font-size: var(--fs-font-size);
    line-height: 1;
    border: 1px solid var(--fs-outline);
    border-radius: var(--fs-radius-btn);
    background: var(--fs-bg);      /* fully opaque */
    color: #fff;
    cursor: pointer;
    transition: background-color .15s ease, transform .02s ease, border-color .15s ease;
  }}
  .{kFsBtnClass} svg {{
    width: calc(18px * var(--fs-icon-scale));
    height: calc(18px * var(--fs-icon-scale));
    flex: 0 0 auto;
  }}
  .{kFsBtnClass}:hover {{
    background: var(--fs-bg-hover); /* lighter, still opaque */
  }}
  .{kFsBtnClass}:active {{
    background: var(--fs-bg-active); /* still opaque */
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

        // Keep for things like outline where alpha matters
        static string CssColorFromUnity(Color c)
        {
            float r = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.r));
            float g = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.g));
            float b = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.b));
            float a = Mathf.Clamp01(c.a);

            int R = Mathf.RoundToInt(r * 255f);
            int G = Mathf.RoundToInt(g * 255f);
            int B = Mathf.RoundToInt(b * 255f);

            return $"rgba({R},{G},{B},{a.ToString(CultureInfo.InvariantCulture)})";
        }

        // Force fully opaque rgba from Unity color (sRGB converted)
        static string CssOpaqueColorFromUnity(Color c)
        {
            float r = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.r));
            float g = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.g));
            float b = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.b));

            int R = Mathf.RoundToInt(r * 255f);
            int G = Mathf.RoundToInt(g * 255f);
            int B = Mathf.RoundToInt(b * 255f);

            return $"rgba({R},{G},{B},1)";
        }

        // Lighten toward white by t (0..1), keep opaque
        static string CssLightenOpaque(Color c, float t)
        {
            t = Mathf.Clamp01(t);
            float r = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.r));
            float g = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.g));
            float b = Mathf.Clamp01(Mathf.LinearToGammaSpace(c.b));

            r += (1f - r) * t;
            g += (1f - g) * t;
            b += (1f - b) * t;

            int R = Mathf.RoundToInt(r * 255f);
            int G = Mathf.RoundToInt(g * 255f);
            int B = Mathf.RoundToInt(b * 255f);

            return $"rgba({R},{G},{B},1)";
        }
    }
}