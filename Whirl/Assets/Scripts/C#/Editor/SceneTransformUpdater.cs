using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(Transform))]
public class EditorTransformUpdater : Editor
{
    // Private
    private Main main;
    private Transform sceneTransform;

    private void OnEnable() => EditorApplication.update += EditorUpdate;

    private void OnDisable() => EditorApplication.update -= EditorUpdate;

    private void EditorUpdate()
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        if (sceneTransform == null) sceneTransform = GameObject.FindGameObjectWithTag("SceneManager").GetComponent<Transform>();

        Vector2 boundaryDims = new(main.BoundaryDims.x, main.BoundaryDims.y);
        sceneTransform.transform.localPosition = boundaryDims * 0.5f;
        sceneTransform.transform.localScale = boundaryDims;
    }
}