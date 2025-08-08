// UIController.cs
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
    public List<Image>      outlines   = new();
    public List<Image>      backgrounds= new();
    public List<Image>      interacts  = new();
    public List<Image>      contrasts  = new();
    public List<UIGradient> interactGradients = new();

    public List<TMP_Text>   texts     = new();

    public List<TMP_Text> header1   = new();
    public List<TMP_Text> header2   = new();
    public List<TMP_Text> body1     = new();
    public List<TMP_Text> body2     = new();
    public List<TMP_Text> intHeader = new();
    public List<TMP_Text> intSlider = new();
    public List<TMP_Text> intField  = new();

    [SerializeField] UIManager uiManager;

    readonly Dictionary<Image,      Color>    imgOrig  = new();
    readonly Dictionary<UIGradient, Gradient> gradOrig = new();
    readonly Dictionary<TMP_Text,   OriginalTextData> txtOrig = new();

    struct OriginalTextData
    {
        public Color         color;
        public TMP_FontAsset font;
        public FontStyles    style;
        public float         size;
        public float charSpace, wordSpace, lineSpace, paraSpace;
    }

#if UNITY_EDITOR
    bool   refreshQueued;
    double nextCheckTime;
    const  double CHECK_INTERVAL = 0.05;
    int    cachedHash;

    void OnEnable()
    {
        if (Application.isPlaying) return;
        EditorApplication.update += EditorUpdate;
        Undo.undoRedoPerformed    += QueueRefresh;
        cachedHash = ComputeManagerHash();
        QueueRefresh();
    }
    void OnDisable()
    {
        if (Application.isPlaying) return;
        EditorApplication.update -= EditorUpdate;
        Undo.undoRedoPerformed   -= QueueRefresh;
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
                h = h * 31 + p.textColor.GetHashCode();
                h = h * 31 + p.contrast.GetHashCode();
                h = h * 31 + p.interactGradient.GetHashCode();
                h = h * 31 + (p.name?.GetHashCode() ?? 0);
            }
            foreach (var f in uiManager.fontPalettes) h = h * 31 + f.GetHashCode();
            return h;
        }
    }
#endif

    void ApplyActivePalettes()
    {
        ColorPalette cp = uiManager.ActivePalette;
        FontPalette  fp = uiManager.ActiveFontPalette;

        ApplyColour(outlines,        cp.outline);
        ApplyColour(backgrounds,     cp.background);
        ApplyColour(interacts,       cp.interactColor);
        ApplyColour(contrasts,       cp.contrast);
        ApplyGradient(interactGradients, cp.interactGradient);

        ApplyColour(texts,     cp.textColor);
        ApplyColour(header1,   cp.textColor);
        ApplyColour(header2,   cp.textColor);
        ApplyColour(body1,     cp.textColor);
        ApplyColour(body2,     cp.textColor);
        ApplyColour(intHeader, cp.textColor);
        ApplyColour(intSlider, cp.textColor);
        ApplyColour(intField,  cp.textColor);

        ApplyFont(header1, fp.header1);   ApplyFont(header2, fp.header2);
        ApplyFont(body1,   fp.body1);     ApplyFont(body2,   fp.body2);
        ApplyFont(intHeader, fp.intHeader); ApplyFont(intSlider, fp.intSlider);
        ApplyFont(intField,  fp.intField);
    }

    void ApplyColour(List<Image> imgs, Color c)
    {
        if (imgs == null) return;
        foreach (var img in imgs)
        {
            if (!img) continue;
            if (!imgOrig.ContainsKey(img)) imgOrig[img] = img.color;
            img.color = c;
        }
    }
    void ApplyColour(List<TMP_Text> lbls, Color c)
    {
        if (lbls == null) return;
        foreach (var lbl in lbls)
        {
            if (!lbl) continue;
            CaptureTextOrig(lbl);
            lbl.color = c;
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

    void ApplyFont(List<TMP_Text> lbls, FontSettings fs)
    {
        if (lbls == null) return;
        foreach (var lbl in lbls)
        {
            if (!lbl) continue;
            CaptureTextOrig(lbl);

            if (fs.overrideFontAsset && fs.fontAsset) lbl.font = fs.fontAsset;
            if (fs.overrideFontStyle)                 lbl.fontStyle  = fs.fontStyle;
            if (fs.overrideFontSize)                  lbl.fontSize   = fs.fontSize;

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
            color      = lbl.color,
            font       = lbl.font,
            style      = lbl.fontStyle,
            size       = lbl.fontSize,
            charSpace  = lbl.characterSpacing,
            wordSpace  = lbl.wordSpacing,
            lineSpace  = lbl.lineSpacing,
            paraSpace  = lbl.paragraphSpacing
        };
    }

    void RestoreIfDetached()
    {
        RestoreImages();
        RestoreGradients();
        RestoreTexts();
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
                    lbl.color            = o.color;
                    lbl.font             = o.font;
                    lbl.fontStyle        = o.style;
                    lbl.fontSize         = o.size;
                    lbl.characterSpacing = o.charSpace;
                    lbl.wordSpacing      = o.wordSpace;
                    lbl.lineSpacing      = o.lineSpace;
                    lbl.paragraphSpacing = o.paraSpace;
                }
                txtOrig.Remove(lbl);
            }
        }
    }

    bool IsInAnyImageList(Image img) =>
        outlines.Contains(img)  || backgrounds.Contains(img) ||
        interacts.Contains(img) || contrasts.Contains(img);

    bool IsInAnyTextList(TMP_Text lbl) =>
        texts.Contains(lbl)  || header1.Contains(lbl) || header2.Contains(lbl) ||
        body1.Contains(lbl)  || body2.Contains(lbl)   ||
        intHeader.Contains(lbl) || intSlider.Contains(lbl) || intField.Contains(lbl);
}