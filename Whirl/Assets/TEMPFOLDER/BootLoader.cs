using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class BootLoader : MonoBehaviour
{
    [SerializeField] string mainSceneName = "Main";
    [SerializeField] Slider progressBar;
    [SerializeField] float minimumShowTime = 0.75f;

    IEnumerator Start()
    {
        Time.timeScale = 1f; // in case you ever reload with a paused timescale

        var startTime = Time.realtimeSinceStartup;
        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(mainSceneName, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f)
        {
            if (progressBar) progressBar.value = Mathf.Clamp01(op.progress / 0.9f);
            yield return null;
        }

        if (progressBar) progressBar.value = 1f;

        float leftover = minimumShowTime - (Time.realtimeSinceStartup - startTime);
        if (leftover > 0f) yield return new WaitForSecondsRealtime(leftover);

        op.allowSceneActivation = true;
    }
}