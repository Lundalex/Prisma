// ─────────────────────────────────────────────────────────────────────────────
// UserSettingsRelayHelper.cs
// - NO C# properties (get/set). We watch the 4 serialized fields every frame.
// - When a field changes (e.g., via Inspector), we write into UserSettings SO.
// - Also exposes 4 UserSelectorInput refs and sets their indices on Start.
// - Keeps the public Set* methods (they set the backing fields and sync).
// ─────────────────────────────────────────────────────────────────────────────
using System;
using UnityEngine;

[ExecuteAlways]
public class UserSettingsRelayHelper : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The UserSettings asset/instance to write to when values change.")]
    public UserSettings userSettings;

    [Header("Selector Inputs (optional)")]
    [Tooltip("Selector for TextSize. On Start, its index is set from current settings.")]
    [SerializeField] private UserSelectorInput textSizeSelector;
    [Tooltip("Selector for LineSpacing. On Start, its index is set from current settings.")]
    [SerializeField] private UserSelectorInput lineSpacingSelector;
    [Tooltip("Selector for Theme. On Start, its index is set from current settings.")]
    [SerializeField] private UserSelectorInput themeSelector;
    [Tooltip("Selector for DyslexiaMode. On Start, its index is set from current settings.")]
    [SerializeField] private UserSelectorInput dyslexiaModeSelector;

    [Header("Values (edited in Inspector)")]
    public TextSize     _textSize;
    public LineSpacing  _lineSpacing;
    public Theme        _theme;
    public DyslexiaMode _dyslexiaMode;

    private TextSize     _prevTextSize;
    private LineSpacing  _prevLineSpacing;
    private Theme        _prevTheme;
    private DyslexiaMode _prevDyslexiaMode;

#if UNITY_EDITOR
    private bool _initialized;
#endif

    private void Start()
    {
        var currentTextSize     = userSettings != null ? userSettings.textSize     : _textSize;
        var currentLineSpacing  = userSettings != null ? userSettings.lineSpacing  : _lineSpacing;
        var currentTheme        = userSettings != null ? userSettings.theme        : _theme;
        var currentDyslexiaMode = userSettings != null ? userSettings.dyslexiaMode : _dyslexiaMode;

        if (textSizeSelector     != null) textSizeSelector.SetSelectorIndex(EnumToIndex(currentTextSize));
        if (lineSpacingSelector  != null) lineSpacingSelector.SetSelectorIndex(EnumToIndex(currentLineSpacing));
        if (themeSelector        != null) themeSelector.SetSelectorIndex(EnumToIndex(currentTheme));
        if (dyslexiaModeSelector != null) dyslexiaModeSelector.SetSelectorIndex(EnumToIndex(currentDyslexiaMode));
    }

    private void OnEnable()
    {
        _prevTextSize = _textSize;
        _prevLineSpacing = _lineSpacing;
        _prevTheme = _theme;
        _prevDyslexiaMode = _dyslexiaMode;
#if UNITY_EDITOR
        _initialized = true;
#endif
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!_initialized)
        {
            _prevTextSize     = _textSize;
            _prevLineSpacing  = _lineSpacing;
            _prevTheme        = _theme;
            _prevDyslexiaMode = _dyslexiaMode;
        }
    }
#endif

    private void Update()
    {
        if (_textSize != _prevTextSize)
        {
            _prevTextSize = _textSize;
            if (userSettings != null) userSettings.textSize = _textSize;
            if (textSizeSelector != null) textSizeSelector.SetSelectorIndex(EnumToIndex(_textSize));
        }

        if (_lineSpacing != _prevLineSpacing)
        {
            _prevLineSpacing = _lineSpacing;
            if (userSettings != null) userSettings.lineSpacing = _lineSpacing;
            if (lineSpacingSelector != null) lineSpacingSelector.SetSelectorIndex(EnumToIndex(_lineSpacing));
        }

        if (_theme != _prevTheme)
        {
            _prevTheme = _theme;
            if (userSettings != null) userSettings.theme = _theme;
            if (themeSelector != null) themeSelector.SetSelectorIndex(EnumToIndex(_theme));
        }

        if (_dyslexiaMode != _prevDyslexiaMode)
        {
            _prevDyslexiaMode = _dyslexiaMode;
            if (userSettings != null) userSettings.dyslexiaMode = _dyslexiaMode;
            if (dyslexiaModeSelector != null) dyslexiaModeSelector.SetSelectorIndex(EnumToIndex(_dyslexiaMode));
        }
    }

    public void SetTextSize(TextSize size)
    {
        _textSize = size;
        if (userSettings != null) userSettings.textSize = _textSize;
        if (textSizeSelector != null) textSizeSelector.SetSelectorIndex(EnumToIndex(_textSize));
        _prevTextSize = _textSize;
    }

    public void SetLineSpacing(LineSpacing spacing)
    {
        _lineSpacing = spacing;
        if (userSettings != null) userSettings.lineSpacing = _lineSpacing;
        if (lineSpacingSelector != null) lineSpacingSelector.SetSelectorIndex(EnumToIndex(_lineSpacing));
        _prevLineSpacing = _lineSpacing;
    }

    public void SetTheme(Theme theme)
    {
        _theme = theme;
        if (userSettings != null) userSettings.theme = _theme;
        if (themeSelector != null) themeSelector.SetSelectorIndex(EnumToIndex(_theme));
        _prevTheme = _theme;
    }

    public void SetDyslexiaMode(DyslexiaMode mode)
    {
        _dyslexiaMode = mode;
        if (userSettings != null) userSettings.dyslexiaMode = _dyslexiaMode;
        if (dyslexiaModeSelector != null) dyslexiaModeSelector.SetSelectorIndex(EnumToIndex(_dyslexiaMode));
        _prevDyslexiaMode = _dyslexiaMode;
    }

    private static int EnumToIndex<T>(T enumValue) where T : Enum
    {
        return Convert.ToInt32(enumValue);
    }
}