using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PM = ProgramManager;

public abstract class UserUIElement : EditorLifeCycle
{
    public PointerHoverArea pointerHoverArea;
    public Image containerTrimImage;
    public TMP_Text title;

    public string titleText;
    [ColorUsage(true, true)] public Color primaryColor;

    [Header("Unity Event")]
    public UnityEvent onValueChanged;

#if UNITY_EDITOR
    public override void OnEditorUpdate()
    {
        if (!Application.isPlaying)
        {
            if (title != null) title.text = titleText;
            InitDisplay();
        }
    }
#endif

    private void Start()
    {
        PM.Instance.AddUserInput(this);
        if (title != null) title.text = titleText;
        InitDisplay();
    }

    public abstract void InitDisplay();
}