using Michsky.MUIP;
using UnityEngine;
using UnityEngine.Events;

[ExecuteInEditMode]
public class UserButtonInput : UserUIElement
{
    [Header("Button")]
    [SerializeField] private float textSize = 25.0f;
    [SerializeField] private string text = "Button";

    [Header("References")]
    [SerializeField] private ButtonManager button;

    [Header("Unity Event")]
    [SerializeField] private UnityEvent onButtonClicked;

    public void OnClick()
    {
        onButtonClicked.Invoke();
        onValueChanged.Invoke();

        if (!Application.isPlaying) InitDisplay();
    }

    public override void InitDisplay()
    {
        button.textSize = textSize;
        button.SetText(text);
    }
}