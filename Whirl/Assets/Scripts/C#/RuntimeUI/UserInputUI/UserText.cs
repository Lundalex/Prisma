using TMPro;
using UnityEngine;

[ExecuteInEditMode]
public class UserText : UserUIElement
{
    [Header("Display")]
    [SerializeField, TextArea(3,4)] private string displayText;
    [SerializeField] private float displayTextSize = 17;

    [Header("References")]
    [SerializeField] private TMP_Text textField;

    private void Update()
    {
        if (!Application.isPlaying) InitDisplay();
    }

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