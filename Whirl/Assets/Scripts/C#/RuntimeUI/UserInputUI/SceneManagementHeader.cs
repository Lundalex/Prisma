using UnityEngine;

[ExecuteAlways]
public class SceneManagementHeader : MonoBehaviour
{
    public GameObject sceneResetButton;
    public GameObject taskSelector;
    public GameObject workspaceView;

    void Update()
    {
        if (workspaceView && sceneResetButton && taskSelector)
        {
            bool isFullscreen = Screen.fullScreen; // DEOS NOT WORK IN WEBGL/WEBGPU BUILDS
#if UNITY_EDITOR
            isFullscreen = true;
#endif
            bool doEnableSceneReset = !workspaceView.activeSelf && isFullscreen;
            bool doEnableTaskSelector = isFullscreen;

            if (sceneResetButton.activeSelf != doEnableSceneReset) sceneResetButton.SetActive(doEnableSceneReset);
            if (taskSelector.activeSelf != doEnableTaskSelector) taskSelector.SetActive(doEnableTaskSelector);
        }
    }
}