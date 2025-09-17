// FramerHtmlExporter.cs
#if UNITY_EDITOR
using UnityEngine;
using System.Globalization;

public class FramerHtmlExporter : HtmlExporter
{
    // Order (as requested): iframeSrc, buildNow, wrapWidth, wrapHeight, crop, edgeCrop, radius, allowFullscreen, buttonLabel, googleFontFamily, googleFontWeight
    [Tooltip("Full iframe URL (e.g. itch.io embed).")]
    public string iframeSrc = "https://itch.io/embed-upload/14981544?color=333333";

    [Tooltip("Toggle to generate code. Auto-resets to false.")]
    public bool buildNow = false;

    [Tooltip("Wrapper width in pixels (no units).")]
    public float wrapWidth = 980f;

    [Tooltip("Wrapper height in pixels (no units).")]
    public float wrapHeight = 560f;

    [Tooltip("Bottom crop (footer) in pixels.")]
    public float crop = 20f;

    [Tooltip("Outer-edge crop on all sides in pixels.")]
    public float edgeCrop = 5f;

    [Tooltip("Border radius in pixels.")]
    public float radius = 10f;

    [Tooltip("If true, adds fullscreen attributes to the iframe.")]
    public bool allowFullscreen = true;

    [Tooltip("Button label text.")]
    public string buttonLabel = "Helskärm";

    // Derived from buttonLabel (not exposed)
    string buttonAriaLabel => buttonLabel;

    [Tooltip("Google font family.")]
    public GoogleFontFamily googleFontFamily = GoogleFontFamily.Assistant;

    [Tooltip("Google font weight (e.g. 600).")]
    public int googleFontWeight = 600;

    // CSS class names (private constants)
    const string kGameWrapClass = "game-wrap";
    const string kClipClass = "clip";
    const string kIframeClass = "game-iframe";
    const string kControlsClass = "controls";
    const string kFsBtnId = "fs-btn";
    const string kFsBtnClass = "fs-btn";

    void OnValidate()
    {
        if (!buildNow) return;
        buildNow = false;
        if (Application.isPlaying) return;

        BuildAndCopy("===== FRAMER HTML (copied to clipboard) =====");
    }

    [ContextMenu("Build + Copy to Clipboard")]
    void ContextBuild()
    {
        BuildAndCopy("===== FRAMER HTML (copied to clipboard) =====");
    }

    protected override string BuildHtml()
    {
        var inv = CultureInfo.InvariantCulture;

        string wrapWidthPx  = wrapWidth.ToString(inv) + "px";
        string wrapHeightPx = wrapHeight.ToString(inv) + "px";
        string cropPx       = crop.ToString(inv) + "px";
        string edgeCropPx   = edgeCrop.ToString(inv) + "px";
        string radiusPx     = radius.ToString(inv) + "px";

        string fullscreenAttrs = allowFullscreen
            ? "allow=\"fullscreen\" allowfullscreen webkitallowfullscreen"
            : "";

        (string cssFamily, string gfParam) = GetGoogleFont(googleFontFamily);

        string html = $@"
<div class=""{kGameWrapClass}"" style=""--wrap-width:{wrapWidthPx}; --wrap-height:{wrapHeightPx}; --crop:{cropPx}; --edge-crop:{edgeCropPx}; --radius:{radiusPx};"">
  <div class=""{kClipClass}"">
    <iframe
      src=""{iframeSrc}""
      {fullscreenAttrs}
      class=""{kIframeClass}"">
    </iframe>
  </div>

  <div class=""{kControlsClass}"">
    <button id=""{kFsBtnId}"" class=""{kFsBtnClass}"" aria-label=""{buttonAriaLabel}"" onclick=""(function(btn){{
      var wrap = btn.closest('.{kGameWrapClass}');
      if (wrap.requestFullscreen) wrap.requestFullscreen();
      else if (wrap.webkitRequestFullscreen) wrap.webkitRequestFullscreen();
    }})(this)"">
      <svg width=""18"" height=""18"" viewBox=""0 0 24 24"" aria-hidden=""true"">
        <path d=""M4 9V4h5M20 9V4h-5M4 15v5h5M20 15v5h-5""
              fill=""none"" stroke=""currentColor"" stroke-width=""2""
              stroke-linecap=""round"" stroke-linejoin=""round"" />
      </svg>
      {buttonLabel}
    </button>
  </div>
</div>

<style>
  @import url('https://fonts.googleapis.com/css2?family={gfParam}:wght@{googleFontWeight}&display=swap');

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

  /* Expand & offset the iframe to crop ~edgeCrop on all sides */
  .{kIframeClass} {{
    position: absolute;
    top: calc(-1 * var(--edge-crop));
    left: calc(-1 * var(--edge-crop));
    width: calc(100% + (var(--edge-crop) * 2));
    height: calc(100% + var(--crop) + (var(--edge-crop) * 2)); /* footer crop + outer crop */
    border: 0;
  }}

  /* Top-center control container */
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
    font-weight: {googleFontWeight};
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

  /* Fullscreen: fill viewport, keep crops, hide controls */
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

    (string cssFamily, string gfParam) GetGoogleFont(GoogleFontFamily family)
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
}
#endif