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
        bool sceneFound = false;
        int searchCount = 0;
        while (!sceneFound && searchCount < sceneNames.Length)
        {
            string sceneName = sceneNames[searchCount++];
            if (IsSceneInBuildSettings(sceneName))
            {
                sceneFound = true;
                PM.startConfirmationStatus = StartConfirmationStatus.None;
                UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
            }
        }
    }
}