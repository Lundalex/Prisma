using System.Collections;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [Header("Startup Settings")]
    public bool pauseOnStart;
    public bool doShowControlsTip;

    [Header("Editor Settings")]
    public bool darkMode;
    public bool doHideUIInSceneView = true;
    public bool showUserUI;

    [Header("Serialized Fields")] 
    [SerializeField] private Main main;
    [SerializeField] private NotificationManager2 notificationManager;
    [SerializeField] private GameObject darkBackground;
    [SerializeField] public GameObject uiCanvas;
    [SerializeField] private GameObject userUI;
    [SerializeField] private GameObject startConfirmationWindow;

    private void OnValidate()
    {
        #if UNITY_EDITOR
            darkBackground.SetActive(darkMode);
            userUI.SetActive(showUserUI);
        #endif
    }
    
    void Awake()
    {
        PM.Instance.ResetData(pauseOnStart);

        // Make sure the user UI & canvas are shown
        uiCanvas.SetActive(true);
        if (!showUserUI) userUI.SetActive(true);

        // --- Important! --- Without editor script reloading - the star confirmation will NOT show up upon play mode cycles (still behaves corerctly in builds though!)

        if (!PM.hasShownStartConfirmation)
        {
            startConfirmationWindow.SetActive(true);
            darkBackground.SetActive(true);
        }
        else
        {
            startConfirmationWindow.SetActive(false);
            darkBackground.SetActive(false);
        }

        PM.Instance.SubscribeToActions();
    }

    private void Start()
    {
        SetTargetFrameRate();

        PM.Instance.main = main;
        PM.Instance.Start();
    }

    public void SetTargetFrameRate()
    {
        if (main.TargetFrameRate > 0)
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = main.TargetFrameRate;
        }
        else 
        {
            QualitySettings.vSyncCount = 1;
            Application.targetFrameRate = 0;
        }
    }

    private void Update() => PM.Instance.Update();

    public void ResetScene() => PM.Instance.ResetScene();

    public void OnStartConfirmation()
    {
        darkBackground.transform.SetParent(startConfirmationWindow.transform);
        darkBackground.transform.SetSiblingIndex(0);
        PM.hasShownStartConfirmation = true;

        StartCoroutine(StartConfirmationDelayCoroutine());
    }

    private IEnumerator StartConfirmationDelayCoroutine()
    {
        // Process start confirmation
        PM.startConfirmationStatus = StartConfirmationStatus.Waiting;
        yield return new WaitForSeconds(Func.MsToSeconds(PM.msStartConfimationDelay));
        PM.startConfirmationStatus = StartConfirmationStatus.Complete;

        // Show controls tip, unless pauseOnStart == true to make sure the pause tip gets shown
        if (!pauseOnStart)
        {
            yield return new WaitForSeconds(Func.MsToSeconds(PM.msControlsTipDelay));
            if (doShowControlsTip) notificationManager.OpenNotification("ControlsTip");
        }
    }

    private void OnDestroy() => PM.Instance.OnDestroy();
}