// UIManager.cs  (ScriptableObject)
using System.Collections.Generic;
using UnityEngine;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(
    fileName = "UIManagerAsset",
    menuName = "UI/Palette Database",
    order    = 100)]
public class UIManager : ScriptableObject
{
    public List<ColorPalette> palettes     = new();
    public List<FontPalette>  fontPalettes = new();

    [Min(0)] public int activePaletteIndex     = 0;
    [Min(0)] public int activeFontPaletteIndex = 0;

    public ColorPalette ActivePalette =>
        palettes == null || palettes.Count == 0
            ? default
            : palettes[Mathf.Clamp(activePaletteIndex, 0, palettes.Count - 1)];

    public FontPalette ActiveFontPalette =>
        fontPalettes == null || fontPalettes.Count == 0
            ? default
            : fontPalettes[Mathf.Clamp(activeFontPaletteIndex, 0, fontPalettes.Count - 1)];

#if UNITY_EDITOR
    void OnValidate()
    {
        if (palettes     != null && palettes.Count     > 0)
            activePaletteIndex     = Mathf.Clamp(activePaletteIndex,     0, palettes.Count     - 1);

        if (fontPalettes != null && fontPalettes.Count > 0)
            activeFontPaletteIndex = Mathf.Clamp(activeFontPaletteIndex, 0, fontPalettes.Count - 1);
    }
#endif
}

/* ────────── palette-data structs ────────── */
[System.Serializable]
public struct ColorPalette
{
    public string  name;
    [ColorUsage(true, true)] public Color outline;
    [ColorUsage(true, true)] public Color background;
    [ColorUsage(true, true)] public Color interactColor;
    public Gradient interactGradient;
    [ColorUsage(true, true)] public Color text;
}

[System.Serializable]
public struct FontSettings
{
    public bool          overrideFontAsset;
    public TMP_FontAsset fontAsset;

    public bool       overrideFontStyle;
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
    public string       name;
    public FontSettings header1;
    public FontSettings header2;
    public FontSettings body1;
    public FontSettings body2;
    public FontSettings intHeader;
    public FontSettings intSlider;
    public FontSettings intField;
}