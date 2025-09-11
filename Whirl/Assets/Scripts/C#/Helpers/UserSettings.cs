using UnityEngine;

[CreateAssetMenu(fileName = "UserSettings", menuName = "Scriptable Objects/UserSettings")]
public class UserSettings : ScriptableObject
{
    [Header("Values")]
    [SerializeField] private TextSize _textSize = TextSize.Normal;
    [SerializeField] private LineSpacing _lineSpacing = LineSpacing.Normal;
    [SerializeField] private Theme _theme = Theme.Modern;
    [SerializeField] private bool _dyslexiaMode = false;

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
        _textSize = TextSize.Normal;
        _lineSpacing = LineSpacing.Normal;
        _theme = Theme.Modern;
        _dyslexiaMode = false;
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
    Light
}