using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using PM = ProgramManager;

[ExecuteAlways]
public abstract class UserUIElement : EditorLifeCycle
{
    
    public PointerHoverArea pointerHoverArea;
    public Image containerTrimImage;
    [SerializeField] private TMP_Text title;

    [SerializeField] private string titleText;
    public Color primaryColor;

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