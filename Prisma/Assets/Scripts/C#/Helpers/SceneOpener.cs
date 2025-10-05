using UnityEngine;
using PM = ProgramManager;

public class SceneOpener : MonoBehaviour
{
    [SerializeField] private string[] sceneNames;

    bool IsSceneInBuildSettings(string sceneName)
    {
        int sceneCount = UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
        for (int i = 0; i < sceneCount; i++)
        {
            string path = UnityEngine.SceneManagement.SceneUtility.GetScenePathByBuildIndex(i);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            if (name.Equals(sceneName)) return true;
        }
        return false;
    }

    public void OpenScene()
    {
        // Prioritize opening the last open scene
        string lastScene = PM.Instance.lastOpenedScene;
        if (!string.IsNullOrEmpty(lastScene) 
            && System.Array.IndexOf(sceneNames, lastScene) >= 0 
            && IsSceneInBuildSettings(lastScene))
        {
            PM.startConfirmationStatus = StartConfirmationStatus.None;
            UnityEngine.SceneManagement.SceneManager.LoadScene(lastScene);
            return;
        }

        // Otherwise, prioritize opening scenes in order of index from low to high
        int searchCount = 0;
        while (searchCount < sceneNames.Length)
        {
            string sceneName = sceneNames[searchCount++];
            if (IsSceneInBuildSettings(sceneName))
            {
                PM.startConfirmationStatus = StartConfirmationStatus.None;
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
                return;
            }
        }

        Debug.Log("No scenes found in build settings. SceneOpener: " + this.name);
    }
}