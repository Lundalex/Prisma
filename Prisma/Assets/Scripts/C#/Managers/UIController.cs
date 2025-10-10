using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;
using TMPro;
using LeTai.Asset.TranslucentImage;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class UIController : MonoBehaviour
{
    [Header("Color Palette Connections")]
    public List<Image> outlines = new();
    public List<Image> backgrounds = new();
    public List<Image> lightBackgrounds = new();
    public List<Image> contrasts = new();
    public List<Image> lowContrasts = new();
    public List<Image> lowestContrasts = new();
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
    public List<TMP_Text> notificationBodies = new();
    public List<TMP_Text> tooltips = new();
    public List<TMP_Text> interactHeaders = new();
    public List<TMP_Text> interactSliders = new();
    public List<TMP_Text> interactFields = new();

    [Header("UIManager Reference")]
    [SerializeField] UIManager uiManager;

    [Header("Extensions")]
    public UIGlassControllerExtension glassExtension;

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

    bool refreshQueued;
    int cachedHash;

    static int s_lastComputedFrame = -1;
    static int s_sharedHash;

    void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.undoRedoPerformed += QueueRefresh;
            cachedHash = int.MinValue;
            QueueRefresh();
        }
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.undoRedoPerformed -= QueueRefresh;
        }
#endif
    }

    void OnValidate()
    {
        if (!Application.isPlaying) QueueRefresh();
    }

    void QueueRefresh() => refreshQueued = true;

    void Update()
    {
        int frame = Time.frameCount;
        if (s_lastComputedFrame != frame && uiManager)
        {
            s_sharedHash = ComputeManagerHash(uiManager);
            s_lastComputedFrame = frame;
        } 

        int newHash = (s_lastComputedFrame == frame) ? s_sharedHash : 0;

        if (newHash != cachedHash)
        {
            cachedHash = newHash;
            refreshQueued = true;
        }

        if (!refreshQueued) return;
        refreshQueued = false;

        if (!uiManager) return;
        ApplyActivePalettes();
        RestoreIfDetached();
#if UNITY_EDITOR
        if (this) EditorUtility.SetDirty(this);
#endif
    }

    static int ComputeManagerHash(UIManager mgr)
    {
        if (!mgr) return 0;
        unchecked
        {
            int h = 17;
            h = h * 31 + mgr.activePaletteIndex;

            foreach (var p in mgr.colorPalettes)
            {
                h = h * 31 + p.outline.GetHashCode();
                h = h * 31 + p.background.GetHashCode();
                h = h * 31 + p.interactColor.GetHashCode();
                h = h * 31 + p.interactGradient.GetHashCode();
                h = h * 31 + p.text.GetHashCode();
                h = h * 31 + p.contrast.GetHashCode();
                h = h * 31 + p.lowContrast.GetHashCode();
                h = h * 31 + p.lowestContrast.GetHashCode();
                h = h * 31 + p.notification.GetHashCode();
                h = h * 31 + (p.glassUI ? 1 : 0);
                h = h * 31 + (p.name?.GetHashCode() ?? 0);
            }

            var fp = mgr.ActiveFontPalette;
            h = h * 31 + (fp.name?.GetHashCode() ?? 0);
            h = h * 31 + HashFontSettings(fp.header1);
            h = h * 31 + HashFontSettings(fp.header2);
            h = h * 31 + HashFontSettings(fp.body1);
            h = h * 31 + HashFontSettings(fp.body2);
            h = h * 31 + HashFontSettings(fp.notificationHeader);
            h = h * 31 + HashFontSettings(fp.notificationBody);
            h = h * 31 + HashFontSettings(fp.tooltip);
            h = h * 31 + HashFontSettings(fp.interactHeader);
            h = h * 31 + HashFontSettings(fp.interactSlider);
            h = h * 31 + HashFontSettings(fp.interactField);

            return h;
        }
    }

    static int HashFontSettings(FontSettings fs)
    {
        unchecked
        {
            int h = 23;
            h = h * 31 + (fs.overrideFontAsset ? 1 : 0);
            h = h * 31 + (fs.fontAsset ? fs.fontAsset.GetInstanceID() : 0);
            h = h * 31 + (fs.overrideFontStyle ? 1 : 0);
            h = h * 31 + (int)fs.fontStyle;
            h = h * 31 + (fs.overrideFontSize ? 1 : 0);
            h = h * 31 + fs.fontSize.GetHashCode();
            h = h * 31 + (fs.overrideSpacing ? 1 : 0);
            h = h * 31 + fs.characterSpacing.GetHashCode();
            h = h * 31 + fs.wordSpacing.GetHashCode();
            h = h * 31 + fs.lineSpacing.GetHashCode();
            h = h * 31 + fs.paragraphSpacing.GetHashCode();
            return h;
        }
    }

    void Start()
    {
        if (uiManager) uiManager.SyncFromUserSettingsAndApply();
        ApplyActivePalettes();
    }

    void ApplyActivePalettes()
    {
        if (!uiManager) return;

        ColorPalette cp = uiManager.ActivePalette;
        FontPalette fp = uiManager.ActiveFontPalette;

        ApplyColour(outlines, cp.outline);
        ApplyColour(backgrounds, cp.background);
        ApplyColour(lightBackgrounds, cp.lightBackground);
        ApplyColour(interactColors, cp.interactColor);
        ApplyColour(contrasts, cp.contrast);
        ApplyColour(lowContrasts, cp.lowContrast);
        ApplyColour(lowestContrasts, cp.lowestContrast);
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
        ApplyColour(tooltips, cp.text);
        ApplyColour(interactHeaders, cp.text);
        ApplyColour(interactSliders, cp.text);
        ApplyColour(interactFields, cp.text);

        if (glassExtension != null)
            glassExtension.SetGlassyUIActive(cp.glassUI);

        SetTranslucencyUpdateFrequency(cp.glassUI);

        ApplyFontsHidden(fp);
    }

    void SetTranslucencyUpdateFrequency(bool glassUI)
    {
        TranslucentImageSource TISource = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<TranslucentImageSource>();
        TISource.MaxUpdateRate = glassUI ? 120f : 5f;
    }

    void ApplyFontsHidden(FontPalette fp)
    {
        var set = GatherAllTMPTexts();
        var states = new List<KeyValuePair<TMP_Text, bool>>(set.Count);

        foreach (var t in set)
        {
            if (!t) continue;
            states.Add(new KeyValuePair<TMP_Text, bool>(t, t.enabled));
            t.enabled = false;
        }

        ApplyFont(header1s, fp.header1);
        ApplyFont(header2s, fp.header2);
        ApplyFont(body1s, fp.body1);
        ApplyFont(body2s, fp.body2);
        ApplyFont(notificationHeaders, fp.notificationHeader);
        ApplyFont(notificationBodies, fp.notificationBody);
        ApplyFont(tooltips, fp.tooltip);
        ApplyFont(interactHeaders, fp.interactHeader);
        ApplyFont(interactSliders, fp.interactSlider);
        ApplyFont(interactFields, fp.interactField);

        for (int i = 0; i < states.Count; i++)
        {
            var t = states[i].Key;
            if (t) t.enabled = states[i].Value;
        }
    }

    HashSet<TMP_Text> GatherAllTMPTexts()
    {
        var set = new HashSet<TMP_Text>();
        AddList(set, header1s);
        AddList(set, header2s);
        AddList(set, body1s);
        AddList(set, body2s);
        AddList(set, notificationHeaders);
        AddList(set, notificationBodies);
        AddList(set, tooltips);
        AddList(set, interactHeaders);
        AddList(set, interactSliders);
        AddList(set, interactFields);
        if (texts != null)
        {
            for (int i = 0; i < texts.Count; i++)
            {
                var tt = texts[i] as TMP_Text;
                if (tt) set.Add(tt);
            }
        }
        return set;
    }

    static void AddList(HashSet<TMP_Text> set, List<TMP_Text> list)
    {
        if (list == null) return;
        for (int i = 0; i < list.Count; i++)
        {
            var t = list[i];
            if (t) set.Add(t);
        }
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
                lbl.wordSpacing = fs.wordSpacing;
                lbl.lineSpacing = fs.lineSpacing;
                lbl.paragraphSpacing = fs.paragraphSpacing;
            }
        }
    }

    void CaptureTextOrig(TMP_Text lbl)
    {
        if (txtOrig.ContainsKey(lbl)) return;
        txtOrig[lbl] = new OriginalTextData
        {
            color = lbl.color,
            font = lbl.font,
            style = lbl.fontStyle,
            size = lbl.fontSize,
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
                    lbl.color = o.color;
                    lbl.font = o.font;
                    lbl.fontStyle = o.style;
                    lbl.fontSize = o.size;
                    lbl.characterSpacing = o.charSpace;
                    lbl.wordSpacing = o.wordSpace;
                    lbl.lineSpacing = o.lineSpace;
                    lbl.paragraphSpacing = o.paraSpace;
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
        lightBackgrounds.Contains(img) || interactColors.Contains(img) ||
        contrasts.Contains(img) || lowContrasts.Contains(img) || lowestContrasts.Contains(img) ||
        notifications.Contains(img);

    bool IsInAnyTextListGraphic(MaskableGraphic g) =>
        texts.Contains(g) || header1s.Contains(g as TMP_Text) || header2s.Contains(g as TMP_Text) ||
        body1s.Contains(g as TMP_Text) || body2s.Contains(g as TMP_Text) ||
        notificationHeaders.Contains(g as TMP_Text) || notificationBodies.Contains(g as TMP_Text) ||
        tooltips.Contains(g as TMP_Text) ||
        interactHeaders.Contains(g as TMP_Text) || interactSliders.Contains(g as TMP_Text) || interactFields.Contains(g as TMP_Text);

    bool IsInAnyTextList(TMP_Text t) =>
        texts.Contains(t) || header1s.Contains(t) || header2s.Contains(t) ||
        body1s.Contains(t) || body2s.Contains(t) ||
        notificationHeaders.Contains(t) || notificationBodies.Contains(t) ||
        tooltips.Contains(t) ||
        interactHeaders.Contains(t) || interactSliders.Contains(t) || interactFields.Contains(t);
}