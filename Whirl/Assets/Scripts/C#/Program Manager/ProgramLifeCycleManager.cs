using System.Collections;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class ProgramLifeCycleManager : MonoBehaviour
{
    [Header("Editor Settings")]
    public bool darkMode;
    public bool showUserUI;

    [Header("Serialized Fields")]
    [SerializeField] private Main main;
    [SerializeField] private NotificationManager2 notificationManager;
    [SerializeField] private GameObject darkBackground;
    [SerializeField] private GameObject userUI;
    [SerializeField] private GameObject startConfirmationWindow;

    private void OnValidate()
    {
        #if UNITY_EDITOR
            darkBackground.SetActive(darkMode);
            userUI.SetActive(showUserUI);
        #endif
    }
    
    private void Awake()
    {
        PM.Instance.ResetData();

        // Make sure the user UI is shown
        if (!showUserUI) userUI.SetActive(true);

        // Show start confirmation (only when starting the program)
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

    private void SetTargetFrameRate()
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

        // Show controls tip
        yield return new WaitForSeconds(Func.MsToSeconds(PM.msControlsTipDelay));
        notificationManager.OpenNotification("ControlsTip");
    }

    private void OnDestroy() => PM.Instance.UnsubscribeFromActions();
}