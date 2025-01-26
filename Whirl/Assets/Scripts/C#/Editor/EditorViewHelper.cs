using UnityEngine;
using UnityEditor;

[InitializeOnLoad]
public class EditorViewHelper : Editor
{
    // References
    [SerializeField] private static Main main;
    [SerializeField] private static ProgramLifeCycleManager lifeCycleManager;
    [SerializeField] private static Transform sceneManagerTransform;
    [SerializeField] private static GameObject uiCanvasObject;

    static EditorViewHelper()
    {
        EditorApplication.update += OnEditorUpdate;
    }

    ~EditorViewHelper()
    {
        EditorApplication.update -= OnEditorUpdate;
    }

    private static void CacheReferences()
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera")?.GetComponent<Main>();
        if (lifeCycleManager == null) lifeCycleManager = GameObject.FindGameObjectWithTag("LifeCycleManager")?.GetComponent<ProgramLifeCycleManager>();
        if (sceneManagerTransform == null) sceneManagerTransform = GameObject.FindGameObjectWithTag("SceneManager")?.GetComponent<Transform>();
        if (uiCanvasObject == null && lifeCycleManager != null)
        {
            uiCanvasObject = lifeCycleManager.uiCanvas;
        }
    }

    private static void OnEditorUpdate()
    {
        CacheReferences();

        if (main != null && sceneManagerTransform != null)
        {
            Vector2 boundaryDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
            sceneManagerTransform.transform.localPosition = boundaryDims * 0.5f;
            sceneManagerTransform.transform.localScale = boundaryDims;
        }

        if (uiCanvasObject != null) uiCanvasObject.SetActive(!(CheckSceneViewActive() && lifeCycleManager.doHideUIInSceneView));
    }

    private static bool CheckSceneViewActive()
    {
        bool anyActive = false;
        foreach (SceneView window in SceneView.sceneViews)
        {
            if (window.hasFocus)
            {
                anyActive = true;
                break;
            }
        }

        return anyActive;
    }
}