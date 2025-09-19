using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Serialization;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "UIManagerAsset",
    menuName = "UI/Palette Database",
    order    = 100)]
public class UIManager : ScriptableObject
{
    public UserSettings userSettings;
    public Michsky.MUIP.UIManager muipUIManager;
    /* ─────────────────────────────────────────────────────────────────────────────
       COLOR PALETTES
    ───────────────────────────────────────────────────────────────────────────── */
    [FormerlySerializedAs("palettes")]
    public List<ColorPalette> colorPalettes = new();

    [Min(0), FormerlySerializedAs("activePaletteIndex")]
    public int activeColorPaletteIndex = 0;

    public ColorPalette ActiveColorPalette =>
        colorPalettes == null || colorPalettes.Count == 0
            ? default
            : colorPalettes[Mathf.Clamp(activeColorPaletteIndex, 0, colorPalettes.Count - 1)];

    public int activePaletteIndex => activeColorPaletteIndex;
    public ColorPalette ActivePalette => ActiveColorPalette;

    /* ─────────────────────────────────────────────────────────────────────────────
       FONT: MULTIPLE SUB-PALETTES + ACTIVE INDEX FOR EACH TYPE
    ───────────────────────────────────────────────────────────────────────────── */
    [Header("Font: Sub Palettes")]
    public List<FontAssetSubPalette>   fontAssetPalettes   = new();
    public List<FontStyleSubPalette>   fontStylePalettes   = new();
    public List<FontSizeSubPalette>    fontSizePalettes    = new();
    public List<FontSpacingSubPalette> fontSpacingPalettes = new();

    [Min(0)] public int activeFontAssetSubPaletteIndex   = 0;
    [Min(0)] public int activeFontStyleSubPaletteIndex   = 0;
    [Min(0)] public int activeFontSizeSubPaletteIndex    = 0;
    [Min(0)] public int activeFontSpacingSubPaletteIndex = 0;

    /// <summary>Active composed font palette used by UIController & runtime.</summary>
    public FontPalette ActiveFontPalette => ComposeActiveFont();

    /* ─────────────────────────────────────────────────────────────────────────────
       EDITOR PREVIEW
    ───────────────────────────────────────────────────────────────────────────── */
    [Header("Composed Font (Preview)")]
    [Tooltip("Editor-only preview; changes to this field will be overwritten.")]
    public FontPalette composedFontPreview;

#if UNITY_EDITOR
    int _cachedHash;
    bool _editorHooked;

    void OnEnable()
    {
        if (Application.isPlaying) return;
        if (!_editorHooked)
        {
            _editorHooked = true;
            _cachedHash = 0;
            EditorApplication.update += EditorTick;
        }
    }
    void OnDisable()
    {
        if (!_editorHooked) return;
        _editorHooked = false;
        EditorApplication.update -= EditorTick;
    }
    void EditorTick()
    {
        int h = ComputeHash();
        if (h != _cachedHash)
        {
            _cachedHash = h;
            composedFontPreview = ComposeActiveFont();

            ApplyTooltipToMUIP(composedFontPreview);

            if (this) EditorUtility.SetDirty(this);
        }
    }
#endif

    public void UpdateAll(int textSizeIdx, int lineSpacingIdx, int themeIdx, bool dyslexiaMode)
    {
        activeFontSizeSubPaletteIndex    = ClampIndex(textSizeIdx,    fontSizePalettes);
        activeFontSpacingSubPaletteIndex = ClampIndex(lineSpacingIdx, fontSpacingPalettes);
        activeColorPaletteIndex          = ClampIndex(themeIdx,       colorPalettes);
        activeFontAssetSubPaletteIndex   = ResolveFontAssetIndex(dyslexiaMode);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            composedFontPreview = ComposeActiveFont();
            ApplyTooltipToMUIP(composedFontPreview);
            EditorUtility.SetDirty(this);
        }
#endif
        if (Application.isPlaying)
        {
            ApplyActiveFontToMUIP();
        }
    }

    public void UpdateAll(TextSize textSize, LineSpacing lineSpacing, Theme theme, bool dyslexiaMode)
    {
        UpdateAll((int)textSize, (int)lineSpacing, (int)theme, dyslexiaMode);
    }

    // Helpers for UpdateAll
    int ClampIndex<T>(int idx, List<T> list)
    {
        int count = list == null ? 0 : list.Count;
        return Mathf.Clamp(idx, 0, Mathf.Max(0, count - 1));
    }

    int ResolveFontAssetIndex(bool dyslexiaMode)
    {
        var list = fontAssetPalettes;
        int count = list == null ? 0 : list.Count;

        if (count == 0) return 0;

        if (!dyslexiaMode)
            return 0; // default palette 

        // Try to find a palette whose name hints at dyslexia (e.g., "Dyslexic", "Dyslexia")
        for (int i = 0; i < count; i++)
        {
            var n = list[i].name;
            if (!string.IsNullOrEmpty(n) && n.Contains("DyslexiaFont_DontChangeName"))
                return i;
        }

        // Otherwise prefer index 1 if it exists, else 0
        return count > 1 ? 1 : 0;
    }

    public void ApplyActiveFontToMUIP()
    {
        var fp = ComposeActiveFont();
        ApplyTooltipToMUIP(fp);
    }

    void ApplyTooltipToMUIP(in FontPalette fp)
    {
        if (muipUIManager == null) return;

        // Assign safely/simple: only apply when values are meaningful.
        if (fp.tooltip.fontAsset != null)
            muipUIManager.tooltipFont = fp.tooltip.fontAsset;

        if (fp.tooltip.fontSize > 0f)
            muipUIManager.tooltipFontSize = fp.tooltip.fontSize;

        if (fp.tooltip.fontAsset != null)
        {
            muipUIManager.selectorFont = fp.tooltip.fontAsset;
            muipUIManager.selectorFont = fp.interactField.fontAsset;
            muipUIManager.inputFieldFont = fp.interactField.fontAsset;
        }
            
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUtility.SetDirty(muipUIManager);
#endif
    }

    /* ─────────────────────────────────────────────────────────────────────────────
       HASHING (poll-based; call this to detect changes)
    ───────────────────────────────────────────────────────────────────────────── */
    public int ComputeHash()
    {
        unchecked
        {
            int h = 17;

            // Color palette index + data
            h = h * 31 + activeColorPaletteIndex;
            if (colorPalettes != null)
            {
                for (int i = 0; i < colorPalettes.Count; i++)
                {
                    var p = colorPalettes[i];
                    h = h * 31 + p.outline.GetHashCode();
                    h = h * 31 + p.background.GetHashCode();
                    h = h * 31 + p.lightBackground.GetHashCode();
                    h = h * 31 + p.interactColor.GetHashCode();
                    h = h * 31 + p.interactGradient.GetHashCode();
                    h = h * 31 + p.text.GetHashCode();
                    h = h * 31 + p.contrast.GetHashCode();
                    h = h * 31 + p.notification.GetHashCode();
                    h = h * 31 + (p.name?.GetHashCode() ?? 0);
                }
            }

            // Active indices for sub-palettes
            h = h * 31 + activeFontAssetSubPaletteIndex;
            h = h * 31 + activeFontStyleSubPaletteIndex;
            h = h * 31 + activeFontSizeSubPaletteIndex;
            h = h * 31 + activeFontSpacingSubPaletteIndex;

            // Lists of sub-palettes
            h = HashList(h, fontAssetPalettes, HashAllTextTypes);
            h = HashList(h, fontStylePalettes, HashAllTextTypes);
            h = HashList(h, fontSizePalettes, HashAllTextTypes);
            h = HashList(h, fontSpacingPalettes, HashAllTextTypes);

            // Also fold in the composed palette to capture cross-sub interactions
            var fp = ComposeActiveFont();
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

    /* ─────────────────────────────────────────────────────────────────────────────
       Composition helpers
    ───────────────────────────────────────────────────────────────────────────── */
    FontPalette ComposeActiveFont()
    {
        var a  = GetActive(fontAssetPalettes,   activeFontAssetSubPaletteIndex);
        var st = GetActive(fontStylePalettes,   activeFontStyleSubPaletteIndex);
        var sz = GetActive(fontSizePalettes,    activeFontSizeSubPaletteIndex);
        var sp = GetActive(fontSpacingPalettes, activeFontSpacingSubPaletteIndex);

        return new FontPalette
        {
            name = ComposeName(a, st, sz, sp),

            header1 = Compose(a.header1, st.header1, sz.header1, sp.header1),
            header2 = Compose(a.header2, st.header2, sz.header2, sp.header2),
            body1   = Compose(a.body1,   st.body1,   sz.body1,   sp.body1),
            body2   = Compose(a.body2,   st.body2,   sz.body2,   sp.body2),

            notificationHeader = Compose(a.notificationHeader, st.notificationHeader, sz.notificationHeader, sp.notificationHeader),
            notificationBody   = Compose(a.notificationBody,   st.notificationBody,   sz.notificationBody,   sp.notificationBody),
            tooltip            = Compose(a.tooltip,            st.tooltip,            sz.tooltip,            sp.tooltip),

            interactHeader = Compose(a.interactHeader, st.interactHeader, sz.interactHeader, sp.interactHeader),
            interactSlider = Compose(a.interactSlider, st.interactSlider, sz.interactSlider, sp.interactSlider),
            interactField  = Compose(a.interactField,  st.interactField,  sz.interactField,  sp.interactField),
        };
    }

    static T GetActive<T>(List<T> list, int index) where T : struct
    {
        if (list == null || list.Count == 0) return default;
        return list[Mathf.Clamp(index, 0, list.Count - 1)];
    }

    static string ComposeName(FontAssetSubPalette a, FontStyleSubPalette s, FontSizeSubPalette z, FontSpacingSubPalette sp)
    {
        string nA  = string.IsNullOrEmpty(a.name)  ? "Assets"   : a.name;
        string nS  = string.IsNullOrEmpty(s.name)  ? "Styles"   : s.name;
        string nZ  = string.IsNullOrEmpty(z.name)  ? "Sizes"    : z.name;
        string nSp = string.IsNullOrEmpty(sp.name) ? "Spacing"  : sp.name;
        return $"Composed: {nA} · {nS} · {nZ} · {nSp}";
    }

    static FontSettings Compose(FontAssetSetting a, FontStyleSetting s, FontSizeSetting z, FontSpacingSetting sp)
    {
        return new FontSettings
        {
            overrideFontAsset = a.overrideFontAsset,
            fontAsset         = a.fontAsset,

            overrideFontStyle = s.overrideFontStyle,
            fontStyle         = s.fontStyle,

            overrideFontSize  = z.overrideFontSize,
            fontSize          = z.fontSize,

            overrideSpacing   = sp.overrideSpacing,
            characterSpacing  = sp.characterSpacing,
            wordSpacing       = sp.wordSpacing,
            lineSpacing       = sp.lineSpacing,
            paragraphSpacing  = sp.paragraphSpacing
        };
    }

    /* ─────────────────────────────────────────────────────────────────────────────
       Hash helpers
    ───────────────────────────────────────────────────────────────────────────── */
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

    static int HashList<T>(int seed, List<T> list, System.Func<int, T, int> itemHasher)
    {
        unchecked
        {
            int h = seed;
            if (list != null)
            {
                for (int i = 0; i < list.Count; i++)
                    h = itemHasher(h, list[i]);
            }
            return h;
        }
    }

    static int HashAllTextTypes(int seed, FontAssetSubPalette sp)
    {
        unchecked
        {
            int h = seed;
            h = h * 31 + (sp.name?.GetHashCode() ?? 0);
            h = h * 31 + Hash(sp.header1);
            h = h * 31 + Hash(sp.header2);
            h = h * 31 + Hash(sp.body1);
            h = h * 31 + Hash(sp.body2);
            h = h * 31 + Hash(sp.notificationHeader);
            h = h * 31 + Hash(sp.notificationBody);
            h = h * 31 + Hash(sp.tooltip);
            h = h * 31 + Hash(sp.interactHeader);
            h = h * 31 + Hash(sp.interactSlider);
            h = h * 31 + Hash(sp.interactField);
            return h;
        }
        static int Hash(FontAssetSetting s)
        {
            unchecked
            {
                int h = 29;
                h = h * 31 + (s.overrideFontAsset ? 1 : 0);
                h = h * 31 + (s.fontAsset ? s.fontAsset.GetInstanceID() : 0);
                return h;
            }
        }
    }

    static int HashAllTextTypes(int seed, FontStyleSubPalette sp)
    {
        unchecked
        {
            int h = seed;
            h = h * 31 + (sp.name?.GetHashCode() ?? 0);
            h = h * 31 + Hash(sp.header1);
            h = h * 31 + Hash(sp.header2);
            h = h * 31 + Hash(sp.body1);
            h = h * 31 + Hash(sp.body2);
            h = h * 31 + Hash(sp.notificationHeader);
            h = h * 31 + Hash(sp.notificationBody);
            h = h * 31 + Hash(sp.tooltip);
            h = h * 31 + Hash(sp.interactHeader);
            h = h * 31 + Hash(sp.interactSlider);
            h = h * 31 + Hash(sp.interactField);
            return h;
        }
        static int Hash(FontStyleSetting s)
        {
            unchecked
            {
                int h = 31;
                h = h * 31 + (s.overrideFontStyle ? 1 : 0);
                h = h * 31 + (int)s.fontStyle;
                return h;
            }
        }
    }

    static int HashAllTextTypes(int seed, FontSizeSubPalette sp)
    {
        unchecked
        {
            int h = seed;
            h = h * 31 + (sp.name?.GetHashCode() ?? 0);
            h = h * 31 + Hash(sp.header1);
            h = h * 31 + Hash(sp.header2);
            h = h * 31 + Hash(sp.body1);
            h = h * 31 + Hash(sp.body2);
            h = h * 31 + Hash(sp.notificationHeader);
            h = h * 31 + Hash(sp.notificationBody);
            h = h * 31 + Hash(sp.tooltip);
            h = h * 31 + Hash(sp.interactHeader);
            h = h * 31 + Hash(sp.interactSlider);
            h = h * 31 + Hash(sp.interactField);
            return h;
        }
        static int Hash(FontSizeSetting s)
        {
            unchecked
            {
                int h = 37;
                h = h * 31 + (s.overrideFontSize ? 1 : 0);
                h = h * 31 + s.fontSize.GetHashCode();
                return h;
            }
        }
    }

    static int HashAllTextTypes(int seed, FontSpacingSubPalette sp)
    {
        unchecked
        {
            int h = seed;
            h = h * 31 + (sp.name?.GetHashCode() ?? 0);
            h = h * 31 + Hash(sp.header1);
            h = h * 31 + Hash(sp.header2);
            h = h * 31 + Hash(sp.body1);
            h = h * 31 + Hash(sp.body2);
            h = h * 31 + Hash(sp.notificationHeader);
            h = h * 31 + Hash(sp.notificationBody);
            h = h * 31 + Hash(sp.tooltip);
            h = h * 31 + Hash(sp.interactHeader);
            h = h * 31 + Hash(sp.interactSlider);
            h = h * 31 + Hash(sp.interactField);
            return h;
        }
        static int Hash(FontSpacingSetting s)
        {
            unchecked
            {
                int h = 41;
                h = h * 31 + (s.overrideSpacing ? 1 : 0);
                h = h * 31 + s.characterSpacing.GetHashCode();
                h = h * 31 + s.wordSpacing.GetHashCode();
                h = h * 31 + s.lineSpacing.GetHashCode();
                h = h * 31 + s.paragraphSpacing.GetHashCode();
                return h;
            }
        }
    }
}

/* ────────── palette-data structs ────────── */
[System.Serializable]
public struct ColorPalette
{
    public string name;

    [ColorUsage(true, true)] public Color outline;
    [ColorUsage(true, true)] public Color background;
    public bool doUseBackgroundGradient;
    [ColorUsage(true, true)] public Color lightBackground;
    [ColorUsage(true, true)] public Color interactColor;
    public Gradient interactGradient;

    [ColorUsage(true, true)] public Color text;
    [ColorUsage(true, true)] public Color contrast;

    [ColorUsage(true, true)] public Color notification;
}

/* ────────── FontSettings + FontPalette (consumed by UIController) ────────── */
[System.Serializable]
public struct FontSettings
{
    public bool overrideFontAsset;
    public TMP_FontAsset fontAsset;

    public bool overrideFontStyle;
    public FontStyles fontStyle;

    public bool  overrideFontSize;
    public float fontSize;

    public bool  overrideSpacing;
    public float characterSpacing;
    public float wordSpacing;
    public float lineSpacing;
    public float paragraphSpacing;
}

[System.Serializable]
public struct FontPalette
{
    public string name;
    public FontSettings header1;
    public FontSettings header2;
    public FontSettings body1;
    public FontSettings body2;
    public FontSettings notificationHeader;
    public FontSettings notificationBody;
    public FontSettings tooltip;
    public FontSettings interactHeader;
    public FontSettings interactSlider;
    public FontSettings interactField;
}

/* ────────── Sub-palette setting structs ────────── */
[System.Serializable]
public struct FontAssetSetting
{
    public bool overrideFontAsset;
    public TMP_FontAsset fontAsset;
}

[System.Serializable]
public struct FontStyleSetting
{
    public bool overrideFontStyle;
    public FontStyles fontStyle;
}

[System.Serializable]
public struct FontSizeSetting
{
    public bool  overrideFontSize;
    public float fontSize;
}

[System.Serializable]
public struct FontSpacingSetting
{
    public bool  overrideSpacing;
    public float characterSpacing;
    public float wordSpacing;
    public float lineSpacing;
    public float paragraphSpacing;
}

/* ────────── Sub-palette containers (per text type) ────────── */
[System.Serializable]
public struct FontAssetSubPalette
{
    public string name;
    public FontAssetSetting header1;
    public FontAssetSetting header2;
    public FontAssetSetting body1;
    public FontAssetSetting body2;
    public FontAssetSetting notificationHeader;
    public FontAssetSetting notificationBody;
    public FontAssetSetting tooltip;
    public FontAssetSetting interactHeader;
    public FontAssetSetting interactSlider;
    public FontAssetSetting interactField;
}

[System.Serializable]
public struct FontStyleSubPalette
{
    public string name;
    public FontStyleSetting header1;
    public FontStyleSetting header2;
    public FontStyleSetting body1;
    public FontStyleSetting body2;
    public FontStyleSetting notificationHeader;
    public FontStyleSetting notificationBody;
    public FontStyleSetting tooltip;
    public FontStyleSetting interactHeader;
    public FontStyleSetting interactSlider;
    public FontStyleSetting interactField;
}

[System.Serializable]
public struct FontSizeSubPalette
{
    public string name;
    public FontSizeSetting header1;
    public FontSizeSetting header2;
    public FontSizeSetting body1;
    public FontSizeSetting body2;
    public FontSizeSetting notificationHeader;
    public FontSizeSetting notificationBody;
    public FontSizeSetting tooltip;
    public FontSizeSetting interactHeader;
    public FontSizeSetting interactSlider;
    public FontSizeSetting interactField;
}

[System.Serializable]
public struct FontSpacingSubPalette
{
    public string name;
    public FontSpacingSetting header1;
    public FontSpacingSetting header2;
    public FontSpacingSetting body1;
    public FontSpacingSetting body2;
    public FontSpacingSetting notificationHeader;
    public FontSpacingSetting notificationBody;
    public FontSpacingSetting tooltip;
    public FontSpacingSetting interactHeader;
    public FontSpacingSetting interactSlider;
    public FontSpacingSetting interactField;
}