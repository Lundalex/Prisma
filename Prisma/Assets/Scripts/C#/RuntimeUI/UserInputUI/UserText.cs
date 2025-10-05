using TMPro;
using UnityEngine;

[ExecuteInEditMode]
public class UserText : UserUIElement
{
    [Header("Display")]
    [SerializeField, TextArea(3,4)] private string displayText;

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
            textField.text = displayText;
        }
        if (containerTrimImage != null) containerTrimImage.color = primaryColor;
    }
}