using Michsky.MUIP;
using UnityEngine;
using PM = ProgramManager;

public class PerformanceChecker : MonoBehaviour
{
    [Header("Activation Criteria")]
    [SerializeField] private bool doSuggestSceneSwitch;
    [SerializeField] private bool doCheckInEditor;
    [SerializeField, Range(10.0f, 60.0f)] private float minFPS;
    [SerializeField, Range(0.1f, 1.0f)] private float minSimSpeed;

    [Header("Notification")]
    [SerializeField] private float activeTime;

    [Header("References")]
    [SerializeField] private NotificationManager switchSceneTip;
    [SerializeField] private MaskedProgressBar maskedProgressBar;

    // Private
    private bool sceneSwitchHasBeenSuggested = false;

    private void Update()
    {
        if (!doSuggestSceneSwitch || sceneSwitchHasBeenSuggested || !PM.Instance.CheckAllowRestart() || (Application.isEditor && !doCheckInEditor)) return;

        float currentFPS = PM.Instance.InterpolatedFPS;
        float currentSimSpeed = PM.Instance.InterpolatedSimSpeed;
        if (currentFPS < minFPS || currentSimSpeed < minSimSpeed)
        {
            switchSceneTip.OpenNotification();
            maskedProgressBar.StartTimer(activeTime);
            sceneSwitchHasBeenSuggested = true;
        }
    }
}
