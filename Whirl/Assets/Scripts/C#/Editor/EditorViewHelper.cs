using UnityEngine;
using UnityEditor;
using LeTai.Asset.TranslucentImage;

[InitializeOnLoad]
public class EditorViewHelper : Editor
{
    // References
    [SerializeField] private static Main main;
    [SerializeField] private static ProgramLifeCycleManager lifeCycleManager;
    [SerializeField] private static Transform sceneManagerTransform;
    [SerializeField] private static Transform rbCameraTransform;
    [SerializeField] private static Camera rbCamera;
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
        if (rbCameraTransform == null) rbCameraTransform = GameObject.FindGameObjectWithTag("RBCamera")?.GetComponent<Transform>();
        if (rbCamera == null) rbCamera = GameObject.FindGameObjectWithTag("RBCamera")?.GetComponent<Camera>();
        if (uiCanvasObject == null && lifeCycleManager != null) uiCanvasObject = lifeCycleManager.uiCanvas;
    }

    private static void OnEditorUpdate()
    {
        CacheReferences();

        if (main != null)
        {
            Vector2 boundaryDims = new(main.BoundaryDims.x, main.BoundaryDims.y);

            if (rbCameraTransform != null && rbCamera != null)
            {
                Vector2 boundaryDims_flipped_offset = new(-boundaryDims.x - 100, boundaryDims.y);
                rbCameraTransform.localPosition = boundaryDims_flipped_offset * 0.5f;
                rbCameraTransform.localScale = boundaryDims;
                rbCamera.orthographicSize = Mathf.Min(main.BoundaryDims.x, main.BoundaryDims.y) * 0.5f;
            }

            if (sceneManagerTransform != null)
            {
                sceneManagerTransform.transform.localPosition = boundaryDims * 0.5f;
                sceneManagerTransform.transform.localScale = boundaryDims;

                ApplyGlobalTranslucentSource();
            }
        }

        if (uiCanvasObject != null) uiCanvasObject.SetActive(!(CheckSceneViewActive() && lifeCycleManager.doHideUIInSceneView));
    }

    // Assign the MainCamera's TranslucentImageSource to every TranslucentImage in loaded scenes
    private static void ApplyGlobalTranslucentSource()
    {
        if (!main) return;

        var source = main.GetComponent<TranslucentImageSource>();
        if (!source)
        {
            Debug.Log("Simulation game object lacks the TranslucentImageSource component.");
            return;
        }

        TranslucentImage[] images;

        #if UNITY_2023_1_OR_NEWER
            images = Object.FindObjectsByType<TranslucentImage>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );
        #else
        
        #pragma warning disable CS0618
            images = Object.FindObjectsOfType<TranslucentImage>(includeInactive: true);
        #pragma warning restore CS0618
        #endif

        foreach (var img in images)
        {
            if (!img || img.source == source) continue;

            #if UNITY_EDITOR
                Undo.RecordObject(img, "Assign Global Translucent Image Source");
            #endif
            img.source = source;
            EditorUtility.SetDirty(img);
        }
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