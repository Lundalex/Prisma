using UnityEngine.EventSystems;
using TMPro;

public static class TMPInputChecker
{
    public static bool UserIsUsingInputField(TMP_InputField specific = null)
    {
        if (EventSystem.current == null) return false;
        var go = EventSystem.current.currentSelectedGameObject;
        if (go == null) return false;
        var activeInput = go.GetComponent<TMP_InputField>();
        if (activeInput == null || !activeInput.isFocused) return false;
        if (specific != null) return ReferenceEquals(activeInput, specific);
        return true;
    }
}