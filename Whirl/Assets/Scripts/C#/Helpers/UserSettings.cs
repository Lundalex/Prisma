using UnityEngine;

[CreateAssetMenu(fileName = "UserSettings", menuName = "Scriptable Objects/UserSettings")]
public class UserSettings : ScriptableObject
{
    public TextSize textSize = TextSize.Normal;
    public LineSpacing lineSpacing = LineSpacing.Normal;
    public Theme theme = Theme.Modern;
    public DyslexiaMode dyslexiaMode = DyslexiaMode.Inactive;
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

public enum DyslexiaMode
{
    Inactive,
    Active
}