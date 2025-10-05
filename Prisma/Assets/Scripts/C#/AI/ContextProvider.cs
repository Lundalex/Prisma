using UnityEngine;

public class ContextProvider : MonoBehaviour
{
    public void ProvideContext(ref CommunicationSettings settings)
    {
        if (settings == null)
        {
            Debug.LogWarning("Cannot use ProvideContext with CommunicationSettings == null. Make sure to always use a valid CommunicationSettings SO for AI-messaging");
            return;
        }

        settings.context = "IF YOU SEE THIS TEXT, BEGIN YOUR ANSWER WITH A RANDOM 2-digit NUMBER!";
    }
}