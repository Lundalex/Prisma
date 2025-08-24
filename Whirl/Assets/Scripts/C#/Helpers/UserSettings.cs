using UnityEngine;

[CreateAssetMenu(fileName = "UserSettings", menuName = "Scriptable Objects/UserSettings")]
public class UserSettings : ScriptableObject
{
    public TextSize textSize = TextSize.Normal;
    public LineSpacing lineSpacing = LineSpacing.Normal;
    public Theme theme = Theme.Modern;

    // Plain boolean now
    public bool dyslexiaMode = false;
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