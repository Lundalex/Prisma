using UnityEngine;
using PM = ProgramManager;

public class SceneOpener : MonoBehaviour
{
    [SerializeField] private string sceneName;
    
    public void OpenScene()
    {
        PM.startConfirmationStatus = StartConfirmationStatus.None;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}