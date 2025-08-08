using UnityEngine.EventSystems;
using TMPro;

public static class TMPInputChecker
{
    public static bool UserIsUsingInputField()
    {
        TMP_InputField activeInput = EventSystem.current.currentSelectedGameObject?.GetComponent<TMP_InputField>();

        if (activeInput != null && activeInput.isFocused) return true;

        return false;
    }
}