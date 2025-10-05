using UnityEngine;
using UnityEngine.Events;
using PM = ProgramManager;

public class PointerHoverEvents : MonoBehaviour
{
    [SerializeField] public UnityEvent OnHoverEnter = new UnityEvent();
    [SerializeField] public UnityEvent OnHoverExit  = new UnityEvent();

    RectTransform rectTransform;
    bool isHovering;

    void Update()
    {
        bool hovering = CheckIfHovering();
        if (hovering != isHovering)
        {
            isHovering = hovering;
            if (isHovering) OnHoverEnter.Invoke();
            else OnHoverExit.Invoke();
        }
    }

    public bool CheckIfHovering()
    {
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();

        // Block hover until the program has started (mirrors your logic)
        if (PM.startConfirmationStatus == StartConfirmationStatus.Waiting ||
            PM.startConfirmationStatus == StartConfirmationStatus.NotStarted)
            return false;

        Vector2 mousePosition = Input.mousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            mousePosition,
            null,
            out Vector2 localPoint);

        return rectTransform.rect.Contains(localPoint);
    }

    void OnDisable()
    {
        if (isHovering)
        {
            isHovering = false;
            OnHoverExit.Invoke();
        }
    }
}