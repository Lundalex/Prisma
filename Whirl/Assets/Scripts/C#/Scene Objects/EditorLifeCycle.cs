using UnityEditor;
using UnityEngine;

public abstract class EditorLifeCycle : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnEnable()
    {
        EditorApplication.update += EditorUpdate;
    }

    private void OnDisable()
    {
        EditorApplication.update -= EditorUpdate;
    }

    private void EditorUpdate() => OnEditorUpdate();

    public abstract void OnEditorUpdate();
#endif
}