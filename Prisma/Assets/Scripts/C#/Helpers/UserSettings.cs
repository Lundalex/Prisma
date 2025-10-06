using UnityEngine;

[CreateAssetMenu(fileName = "UserSettings", menuName = "Scriptable Objects/UserSettings")]
public class UserSettings : ScriptableObject
{
    [Header("Current Values")]
    [SerializeField] private TextSize _textSize = TextSize.Normal;
    [SerializeField] private LineSpacing _lineSpacing = LineSpacing.Normal;
    [SerializeField] private Theme _theme = Theme.Glass;
    [SerializeField] private bool _dyslexiaMode = false;

    [Header("Default Values")]
    [SerializeField] private TextSize defaultTextSize = TextSize.Normal;
    [SerializeField] private LineSpacing defaultLineSpacing = LineSpacing.Normal;
    [SerializeField] private Theme defaultTheme = Theme.Glass;
    [SerializeField] private bool defaultDyslexiaMode = false;

    [Header("References")]
    public UIManager uiManager;

    public TextSize textSize
    {
        get => _textSize;
        set
        {
            if (_textSize == value) return;
            _textSize = value;
            RaiseChanged();
        }
    }

    public LineSpacing lineSpacing
    {
        get => _lineSpacing;
        set
        {
            if (_lineSpacing == value) return;
            _lineSpacing = value;
            RaiseChanged();
        }
    }

    public Theme theme
    {
        get => _theme;
        set
        {
            if (_theme == value) return;
            _theme = value;
            RaiseChanged();
        }
    }

    public bool dyslexiaMode
    {
        get => _dyslexiaMode;
        set
        {
            if (_dyslexiaMode == value) return;
            _dyslexiaMode = value;
            RaiseChanged();
        }
    }

    public void Reset()
    {
        // Use properties so the event fires for each change
        _textSize = defaultTextSize;
        _lineSpacing = defaultLineSpacing;
        _theme = defaultTheme;
        _dyslexiaMode = defaultDyslexiaMode;
        RaiseChanged();
    }

    private void RaiseChanged()
    {
        uiManager.UpdateAll(textSize, lineSpacing, theme, dyslexiaMode);
    }
}

public enum TextSize
{
    Normal,
    Larger,
    Largest
}

public enum LineSpacing
{
    Normal,
    Larger,
    Largest
}

public enum Theme
{
    Modern,
    Dark,
    Light,
    Glass
}