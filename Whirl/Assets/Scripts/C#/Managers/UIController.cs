using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UIController : MonoBehaviour
{
    public bool forceUpdateOnStart = false;

    [Header("Color Palette Connections")]
    public List<Image> outlines = new();
    public List<Image> backgrounds = new();
    public List<Image> contrasts = new();
    public List<Image> interactColors = new();
    public List<UIGradient> interactGradients = new();
    public List<TMP_Text> interactGradientTexts = new();
    public List<MaskableGraphic> texts = new();
    public List<Image> notifications = new();

    [Header("Text Palette Connections")]
    public List<TMP_Text> header1s = new();
    public List<TMP_Text> header2s = new();
    public List<TMP_Text> body1s = new();
    public List<TMP_Text> body2s = new();
    public List<TMP_Text> notificationHeaders = new();
    public List<TMP_Text> notificationBodies   = new();
    public List<TMP_Text> interactHeaders = new();
    public List<TMP_Text> interactSliders = new();
    public List<TMP_Text> interactFields = new();
    
    [Header("UIManager Reference")]
    [SerializeField] UIManager uiManager;

    readonly Dictionary<Image, Color> imgOrig = new();
    readonly Dictionary<UIGradient, Gradient> gradOrig = new();
    readonly Dictionary<TMP_Text, OriginalTextData> txtOrig = new();
    readonly Dictionary<MaskableGraphic, Color> gfxOrig = new();
    readonly Dictionary<TMP_Text, TmpTextGradientState> tmpGradOrig = new();

    struct OriginalTextData
    {
        public Color color;
        public TMP_FontAsset font;
        public FontStyles style;
        public float size;
        public float charSpace, wordSpace, lineSpace, paraSpace;
    }

    struct TmpTextGradientState
    {
        public bool enabled;
        public VertexGradient gradient;
        public TMP_ColorGradient preset;
    }

#if UNITY_EDITOR
    bool refreshQueued;
    double nextCheckTime;
    const double CHECK_INTERVAL = 0.02;
    int cachedHash;

    void OnEnable()
    {
        if (Application.isPlaying) return;
        EditorApplication.update += EditorUpdate;
        Undo.undoRedoPerformed += QueueRefresh;
        cachedHash = ComputeManagerHash();
        QueueRefresh();
    }
    void OnDisable()
    {
        if (Application.isPlaying) return;
        EditorApplication.update -= EditorUpdate;
        Undo.undoRedoPerformed -= QueueRefresh;
    }
    void OnValidate() { if (!Application.isPlaying) QueueRefresh(); }
    void QueueRefresh() => refreshQueued = true;

    void EditorUpdate()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now >= nextCheckTime)
        {
            nextCheckTime = now + CHECK_INTERVAL;
            int newHash = ComputeManagerHash();
            if (newHash != cachedHash) { cachedHash = newHash; refreshQueued = true; }
        }
        if (!refreshQueued) return;
        refreshQueued = false;

        if (!uiManager) return;
        ApplyActivePalettes();
        RestoreIfDetached();
        EditorUtility.SetDirty(this);
    }

    int ComputeManagerHash()
    {
        if (!uiManager) return 0;
        unchecked
        {
            int h = 17;
            h = h * 31 + uiManager.activePaletteIndex;
            h = h * 31 + uiManager.activeFontPaletteIndex;
            foreach (var p in uiManager.palettes)
            {
                h = h * 31 + p.outline.GetHashCode();
                h = h * 31 + p.background.GetHashCode();
                h = h * 31 + p.interactColor.GetHashCode();
                h = h * 31 + p.interactGradient.GetHashCode();
                h = h * 31 + p.text.GetHashCode();
                h = h * 31 + p.contrast.GetHashCode();
                h = h * 31 + p.notification.GetHashCode();
                h = h * 31 + (p.name?.GetHashCode() ?? 0);
            }
            foreach (var f in uiManager.fontPalettes) h = h * 31 + f.GetHashCode();
            return h;
        }
    }
#endif

    void Start()
    {
        if (forceUpdateOnStart)
        {
            ApplyActivePalettes();
        }
    }

    void ApplyActivePalettes()
    {
        if (!uiManager) return;

        ColorPalette cp = uiManager.ActivePalette;
        FontPalette  fp = uiManager.ActiveFontPalette;

        ApplyColour(outlines, cp.outline);
        ApplyColour(backgrounds, cp.background);
        ApplyColour(interactColors, cp.interactColor);
        ApplyColour(contrasts, cp.contrast);
        ApplyGradient(interactGradients, cp.interactGradient);
        ApplyGradient(interactGradientTexts, cp.interactGradient);
        ApplyColour(notifications, cp.notification);

        ApplyColour(texts, cp.text);
        ApplyColour(header1s, cp.text);
        ApplyColour(header2s, cp.text);
        ApplyColour(body1s, cp.text);
        ApplyColour(body2s, cp.text);
        ApplyColour(notificationHeaders, cp.text);
        ApplyColour(notificationBodies, cp.text);
        ApplyColour(interactHeaders, cp.text);
        ApplyColour(interactSliders, cp.text);
        ApplyColour(interactFields, cp.text);

        ApplyFont(header1s,   fp.header1);
        ApplyFont(header2s,   fp.header2);
        ApplyFont(body1s,     fp.body1);
        ApplyFont(body2s,     fp.body2);
        ApplyFont(notificationHeaders, fp.notificationHeader);
        ApplyFont(notificationBodies,   fp.notificationBody);
        ApplyFont(interactHeaders, fp.interactHeader);
        ApplyFont(interactSliders, fp.interactSlider);
        ApplyFont(interactFields,  fp.interactField);
    }

    void ApplyColour(List<Image> imgs, Color c)
    {
        if (imgs == null) return;
        foreach (var img in imgs)
        {
            if (!img) continue;
            if (!imgOrig.ContainsKey(img)) imgOrig[img] = img.color;
            var nc = c; nc.a = img.color.a;
            img.color = nc;
        }
    }

    void ApplyColour(List<MaskableGraphic> gfx, Color c)
    {
        if (gfx == null) return;
        foreach (var g in gfx)
        {
            if (!g) continue;
            var t = g as TMP_Text;
            if (t) CaptureTextOrig(t);
            if (!gfxOrig.ContainsKey(g)) gfxOrig[g] = g.color;
            var nc = c; nc.a = g.color.a;
            g.color = nc;
        }
    }

    void ApplyColour(List<TMP_Text> lbls, Color c)
    {
        if (lbls == null) return;
        foreach (var lbl in lbls)
        {
            if (!lbl) continue;
            CaptureTextOrig(lbl);
            var nc = c; nc.a = lbl.color.a;
            lbl.color = nc;
        }
    }
    void ApplyGradient(List<UIGradient> gs, Gradient g)
    {
        if (gs == null) return;
        foreach (var gr in gs)
        {
            if (!gr) continue;
            if (!gradOrig.ContainsKey(gr)) gradOrig[gr] = gr.EffectGradient;
            gr.EffectGradient = g;
        }
    }
    void ApplyGradient(List<TMP_Text> lbls, Gradient g)
    {
        if (lbls == null) return;
        foreach (var t in lbls)
        {
            if (!t) continue;
            if (!tmpGradOrig.ContainsKey(t))
                tmpGradOrig[t] = new TmpTextGradientState { enabled = t.enableVertexGradient, gradient = t.colorGradient, preset = t.colorGradientPreset };
            var top = g.Evaluate(1f);
            var bottom = g.Evaluate(0f);
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
            t.colorGradientPreset = null;
        }
    }

    void ApplyFont(List<TMP_Text> lbls, FontSettings fs)
    {
        if (lbls == null) return;
        foreach (var lbl in lbls)
        {
            if (!lbl) continue;
            CaptureTextOrig(lbl);

            if (fs.overrideFontAsset && fs.fontAsset) lbl.font = fs.fontAsset;
            if (fs.overrideFontStyle) lbl.fontStyle = fs.fontStyle;
            if (fs.overrideFontSize) lbl.fontSize = fs.fontSize;

            if (fs.overrideSpacing)
            {
                lbl.characterSpacing = fs.characterSpacing;
                lbl.wordSpacing      = fs.wordSpacing;
                lbl.lineSpacing      = fs.lineSpacing;
                lbl.paragraphSpacing = fs.paragraphSpacing;
            }
        }
    }

    void CaptureTextOrig(TMP_Text lbl)
    {
        if (txtOrig.ContainsKey(lbl)) return;
        txtOrig[lbl] = new OriginalTextData
        {
            color     = lbl.color,
            font      = lbl.font,
            style     = lbl.fontStyle,
            size      = lbl.fontSize,
            charSpace = lbl.characterSpacing,
            wordSpace = lbl.wordSpacing,
            lineSpace = lbl.lineSpacing,
            paraSpace = lbl.paragraphSpacing
        };
    }

    void RestoreIfDetached()
    {
        RestoreImages();
        RestoreGradients();
        RestoreTexts();
        RestoreGraphicsColours();
    }
    void RestoreImages()
    {
        var keys = new List<Image>(imgOrig.Keys);
        foreach (var img in keys)
        {
            if (img == null || !IsInAnyImageList(img))
            {
                if (img) img.color = imgOrig[img];
                imgOrig.Remove(img);
            }
        }
    }
    void RestoreGradients()
    {
        var keys = new List<UIGradient>(gradOrig.Keys);
        foreach (var gr in keys)
        {
            if (gr == null || !interactGradients.Contains(gr))
            {
                if (gr) gr.EffectGradient = gradOrig[gr];
                gradOrig.Remove(gr);
            }
        }

        var tkeys = new List<TMP_Text>(tmpGradOrig.Keys);
        foreach (var lbl in tkeys)
        {
            if (lbl == null || !interactGradientTexts.Contains(lbl))
            {
                var s = tmpGradOrig[lbl];
                lbl.enableVertexGradient = s.enabled;
                if (s.preset != null) lbl.colorGradientPreset = s.preset;
                else lbl.colorGradient = s.gradient;
                tmpGradOrig.Remove(lbl);
            }
        }
    }
    void RestoreTexts()
    {
        var keys = new List<TMP_Text>(txtOrig.Keys);
        foreach (var lbl in keys)
        {
            if (lbl == null || !IsInAnyTextList(lbl))
            {
                if (lbl)
                {
                    var o = txtOrig[lbl];
                    lbl.color             = o.color;
                    lbl.font              = o.font;
                    lbl.fontStyle         = o.style;
                    lbl.fontSize          = o.size;
                    lbl.characterSpacing  = o.charSpace;
                    lbl.wordSpacing       = o.wordSpace;
                    lbl.lineSpacing       = o.lineSpace;
                    lbl.paragraphSpacing  = o.paraSpace;
                }
                txtOrig.Remove(lbl);
            }
        }
    }
    void RestoreGraphicsColours()
    {
        var keys = new List<MaskableGraphic>(gfxOrig.Keys);
        foreach (var g in keys)
        {
            if (g == null || !IsInAnyTextListGraphic(g))
            {
                if (g) g.color = gfxOrig[g];
                gfxOrig.Remove(g);
            }
        }
    }

    bool IsInAnyImageList(Image img) =>
        outlines.Contains(img) || backgrounds.Contains(img) ||
        interactColors.Contains(img) || contrasts.Contains(img) ||
        notifications.Contains(img);

    bool IsInAnyTextListGraphic(MaskableGraphic g) =>
        texts.Contains(g)     || header1s.Contains(g as TMP_Text) || header2s.Contains(g as TMP_Text) ||
        body1s.Contains(g as TMP_Text)     || body2s.Contains(g as TMP_Text)   ||
        notificationHeaders.Contains(g as TMP_Text) || notificationBodies.Contains(g as TMP_Text) ||
        interactHeaders.Contains(g as TMP_Text) || interactSliders.Contains(g as TMP_Text) || interactFields.Contains(g as TMP_Text);

    bool IsInAnyTextList(TMP_Text t) =>
        texts.Contains(t)     || header1s.Contains(t) || header2s.Contains(t) ||
        body1s.Contains(t)     || body2s.Contains(t)   ||
        notificationHeaders.Contains(t) || notificationBodies.Contains(t) ||
        interactHeaders.Contains(t) || interactSliders.Contains(t) || interactFields.Contains(t);
}