using UnityEngine;
using PM = ProgramManager;

public class FullscreenView : MonoBehaviour
{
    void OnEnable()
    {
        UpdateState(true);
    }

    void OnDisable()
    {
        UpdateState(false);
    }

    void UpdateState(bool state)
    {
        if (Application.isPlaying)
        {
            if (state) PM.Instance.programPaused = state;
            else PM.Instance.TriggerSetPauseState(state);
        }
    }
}