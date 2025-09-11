using System;
using UnityEngine;

[ExecuteAlways]
public class UserSettingsRelayHelper : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The UserSettings asset/instance to write to when values change.")]
    public UserSettings userSettings;

    [Header("Selector Inputs")]
    [Tooltip("Selector for TextSize. On Start, its index is set from current settings.")]
    [SerializeField] private UserSelectorInput textSizeSelector;
    [Tooltip("Selector for LineSpacing. On Start, its index is set from current settings.")]
    [SerializeField] private UserSelectorInput lineSpacingSelector;
    [Tooltip("Theme controller. On Start, its index is set from current settings.")]
    [SerializeField] private MultiThemeButtonController themeButtonController;

    [Header("Toggle Input")]
    [Tooltip("Toggle for DyslexiaMode. On Start (Play Mode), its state is set from current settings.")]
    [SerializeField] private UserToggleInput dyslexiaModeToggle;

    [Header("Values")]
    public TextSize    _textSize;
    public LineSpacing _lineSpacing;
    public Theme       _theme;
    public bool        _dyslexiaMode;

    private TextSize    _prevTextSize;
    private LineSpacing _prevLineSpacing;
    private Theme       _prevTheme;
    private bool        _prevDyslexiaMode;

#if UNITY_EDITOR
    private bool _initialized;
#endif

    private void Start()
    {
        var currentTextSize     = userSettings != null ? userSettings.textSize     : _textSize;
        var currentLineSpacing  = userSettings != null ? userSettings.lineSpacing  : _lineSpacing;
        var currentTheme        = userSettings != null ? userSettings.theme        : _theme;
        var currentDyslexiaMode = userSettings != null ? userSettings.dyslexiaMode : _dyslexiaMode;

        if (textSizeSelector    != null) textSizeSelector.SetSelectorIndex(EnumToIndex(currentTextSize));
        if (lineSpacingSelector != null) lineSpacingSelector.SetSelectorIndex(EnumToIndex(currentLineSpacing));
        if (themeButtonController != null) themeButtonController.SetSelectedIndex(EnumToIndex(currentTheme));

        // Avoid MUIP UpdateUI in Edit Mode (SwitchManager uses coroutines).
        if (dyslexiaModeToggle != null && Application.isPlaying)
            dyslexiaModeToggle.SetIsOn(currentDyslexiaMode);
    }

    private void OnEnable()
    {
        _prevTextSize     = _textSize;
        _prevLineSpacing  = _lineSpacing;
        _prevTheme        = _theme;
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
        // -------- Text Size (Field -> UI) --------
        if (_textSize != _prevTextSize)
        {
            _prevTextSize = _textSize;
            if (userSettings != null) userSettings.textSize = _textSize;
            if (textSizeSelector != null) textSizeSelector.SetSelectorIndex(EnumToIndex(_textSize));
        }

        // -------- Line Spacing (Field -> UI) --------
        if (_lineSpacing != _prevLineSpacing)
        {
            _prevLineSpacing = _lineSpacing;
            if (userSettings != null) userSettings.lineSpacing = _lineSpacing;
            if (lineSpacingSelector != null) lineSpacingSelector.SetSelectorIndex(EnumToIndex(_lineSpacing));
        }

        // -------- Theme (Field -> UI) --------
        if (_theme != _prevTheme)
        {
            _prevTheme = _theme;
            if (userSettings != null) userSettings.theme = _theme;
            if (themeButtonController != null) themeButtonController.SetSelectedIndex(EnumToIndex(_theme));
        }

        // -------- Theme (UI -> Field) --------
        if (themeButtonController != null)
        {
            int uiIndex = themeButtonController.GetSelectedIndex();
            int fieldIndex = EnumToIndex(_theme);
            if (uiIndex != fieldIndex)
            {
                _theme = (Theme)uiIndex;
                _prevTheme = _theme;
                if (userSettings != null) userSettings.theme = _theme;
            }
        }

        // -------- Dyslexia Mode (Field -> UI) --------
        if (_dyslexiaMode != _prevDyslexiaMode)
        {
            _prevDyslexiaMode = _dyslexiaMode;
            if (userSettings != null) userSettings.dyslexiaMode = _dyslexiaMode;

            // Only drive the MUIP toggle in Play Mode to avoid editor coroutines.
            if (dyslexiaModeToggle != null && Application.isPlaying)
                dyslexiaModeToggle.SetIsOn(_dyslexiaMode);
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
        if (themeButtonController != null) themeButtonController.SetSelectedIndex(EnumToIndex(_theme));
        _prevTheme = _theme;
    }

    public void SetDyslexiaMode(bool active)
    {
        _dyslexiaMode = active;
        if (userSettings != null) userSettings.dyslexiaMode = _dyslexiaMode;

        if (dyslexiaModeToggle != null && Application.isPlaying)
            dyslexiaModeToggle.SetIsOn(_dyslexiaMode);

        _prevDyslexiaMode = _dyslexiaMode;
    }

    private static int EnumToIndex<T>(T enumValue) where T : Enum
    {
        return Convert.ToInt32(enumValue);
    }
}