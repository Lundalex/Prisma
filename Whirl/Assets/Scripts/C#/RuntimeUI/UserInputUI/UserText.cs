using TMPro;
using UnityEngine;

public class UserText : UserUIElement
{
    [Header("Display")]
    [SerializeField, TextArea(3,4)] private string displayText;
    [SerializeField] private float displayTextSize = 17;

    [Header("References")]
    [SerializeField] private TMP_Text textField;

    public override void InitDisplay()
    {
        if (textField != null)
        {
            textField.fontSize = displayTextSize;
            textField.text = displayText;
        }
        containerTrimImage.color = primaryColor;
    }
}