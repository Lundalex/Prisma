using System.Collections;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [Header("Startup Settings")]
    public bool pauseOnStart;
    public bool slowMotionOnStart;
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
    [SerializeField] private GameObject fullscreenView;

    private void OnValidate()
    {
        #if UNITY_EDITOR
            darkBackground.SetActive(darkMode);
            userUI.SetActive(showUserUI);
        #endif
    }
    
    void Awake()
    {
        PM.Instance.ResetData(pauseOnStart, slowMotionOnStart);

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

    public void ResetScene(bool soft_hard = false) => PM.Instance.ResetScene(soft_hard);

    public void PrimeSceneReset_OnSimulationView()
    {
        if (!fullscreenView) fullscreenView = GameObject.FindGameObjectWithTag("FullscreenView");
        if (!fullscreenView.activeInHierarchy)
        {
            PM.Instance.SoftResetScene();
        }
        else
        {
            StartCoroutine(WaitForFullscreenInactiveThenReset());
        }
    }

    public void PrimeSceneReset_OnInteraction()
    {
        if (!fullscreenView) fullscreenView = GameObject.FindGameObjectWithTag("FullscreenView");

        StartCoroutine(WaitForFirstInteractionThenReset());

        IEnumerator WaitForFirstInteractionThenReset()
        {
            // Wait until the app allows restarting (i.e., start confirmation is done)
            yield return new WaitUntil(() => PM.Instance.CheckAllowRestart());

            // If a fullscreen overlay is up, wait for it to close first
            if (fullscreenView && fullscreenView.activeInHierarchy)
                yield return new WaitUntil(() => !fullscreenView.activeInHierarchy);

            // Wait for the first valid scene interaction (not on UI / not while editing inputs)
            bool AllowedSceneClick()
            {
                if (PM.Instance.CheckAnyUIElementHovered()) return false;
                if (PM.Instance.CheckAnySensorBeingMoved()) return false;
                if (PM.Instance.isAnySensorSettingsViewActive) return false;
                if (TMPInputChecker.UserIsUsingInputField()) return false;
                return Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1);
            }

            yield return new WaitUntil(AllowedSceneClick);
            PM.Instance.SoftResetScene();
        }
    }

    private IEnumerator WaitForFullscreenInactiveThenReset()
    {
        yield return new WaitUntil(() => !fullscreenView.activeInHierarchy);
        PM.Instance.SoftResetScene();
    }

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