using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SceneManagementHeader : MonoBehaviour
{
    [Header("Middle Container Positioning")]
    public RectTransform middleContainer;
    public float simulationViewTopOffset;
    public float workspaceViewTopOffset;

    [Header("References")]
    public GameObject sceneResetButton;
    public GameObject taskSelector;
    public GameObject workspaceView;
    public WindowToggle workspaceWindowToggle;
    public GameObject simSpeedButtons;
    public GameObject feedbackButton;

    void OnEnable()
    {
        feedbackButton.SetActive(workspaceView.activeSelf);
        ApplyTopOffset(workspaceView && workspaceView.activeSelf);
#if UNITY_EDITOR
        EditorApplication.update += EditorUpdate;
#endif
    }

    void OnDisable()
    {
#if UNITY_EDITOR
        EditorApplication.update -= EditorUpdate;
#endif
    }

    void Update()
    {
        if (!sceneResetButton || !taskSelector || !workspaceView) return;

        bool isFullscreen = IsFullscreenSafe();

        bool doEnableSceneReset = !workspaceView.activeSelf && isFullscreen;
        bool doEnableTaskSelector = isFullscreen;

        if (sceneResetButton.activeSelf != doEnableSceneReset) sceneResetButton.SetActive(doEnableSceneReset);
        if (taskSelector.activeSelf != doEnableTaskSelector) taskSelector.SetActive(doEnableTaskSelector);
    }

#if UNITY_EDITOR
    private void EditorUpdate()
    {
        if (!Application.isPlaying)
            ApplyTopOffset(workspaceView && workspaceView.activeSelf);
    }
#endif

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
        feedbackButton.SetActive(state);
        ApplyTopOffset(state);
    }

    private void ApplyTopOffset(bool simView)
    {
        if (!middleContainer) return;
        Vector2 om = middleContainer.offsetMax;
        middleContainer.offsetMax = new Vector2(om.x, -(simView ? workspaceViewTopOffset : simulationViewTopOffset));
    }
}