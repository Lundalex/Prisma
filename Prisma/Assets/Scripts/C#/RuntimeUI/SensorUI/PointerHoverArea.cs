using UnityEngine;
using PM = ProgramManager;

public class PointerHoverArea : MonoBehaviour
{
    private RectTransform rectTransform;
    public bool CheckIfHovering()
    {
        if (rectTransform == null) rectTransform = this.GetComponent<RectTransform>();

        // Don't allow hovering when the program is starting up to avoid unintended hovering
        if (PM.startConfirmationStatus == StartConfirmationStatus.Waiting || PM.startConfirmationStatus == StartConfirmationStatus.NotStarted) return false;
        
        
        Vector2 mousePosition = Input.mousePosition;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rectTransform,
            mousePosition,
            null,
            out Vector2 localPoint);

        return rectTransform.rect.Contains(localPoint);
    }
}