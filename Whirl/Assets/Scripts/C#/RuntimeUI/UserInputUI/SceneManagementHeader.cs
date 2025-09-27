using UnityEngine;

[ExecuteAlways]
public class SceneManagementHeader : MonoBehaviour
{
    public GameObject sceneResetButton;
    public GameObject taskSelector;
    public GameObject workspaceView;
    public WindowToggle workspaceWindowToggle;
    public GameObject simSpeedButtons;

    void Update()
    {
        if (!sceneResetButton || !taskSelector || !workspaceView) return;

        bool isFullscreen = IsFullscreenSafe();

        bool doEnableSceneReset = !workspaceView.activeSelf && isFullscreen;
        bool doEnableTaskSelector = isFullscreen;

        if (sceneResetButton.activeSelf != doEnableSceneReset) sceneResetButton.SetActive(doEnableSceneReset);
        if (taskSelector.activeSelf != doEnableTaskSelector) taskSelector.SetActive(doEnableTaskSelector);
    }

    private bool IsFullscreenSafe()
    {
#if UNITY_EDITOR
        return true;
#else
        return WebFullscreen.IsViewportLikelyFullscreen();
#endif
    }

    public void SetFullscreenState(bool state)
    {
        workspaceView.SetActive(state);
        workspaceWindowToggle.SetModeA(!state);
        simSpeedButtons.SetActive(!state);
    }
}