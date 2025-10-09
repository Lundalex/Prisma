using UnityEngine;
using PM = ProgramManager;

[DisallowMultipleComponent]
public class SimSpeedButtons : MonoBehaviour
{
    public enum Mode { Pause, Slow, Fast }

    [Header("UI (A=off, B=on)")]
    [SerializeField] private WindowToggle pauseUI;
    [SerializeField] private WindowToggle slowUI;
    [SerializeField] private WindowToggle fastUI;

    [Header("State")]
    [SerializeField] private Mode currentMode = Mode.Fast;

    private bool? lastPaused;
    private bool? lastSlow;

    private void OnEnable()
    {
        if (PM.Instance != null)
        {
            PM.Instance.OnSetNewPauseState += OnPauseChanged;
            PM.Instance.OnSetNewSlowMotionState += OnSlowChanged;
        }

        SyncFromPMState();
    }

    private void Start()
    {
        // Ensure correct state even if subscription happened before PM finished init
        SyncFromPMState();
    }

    private void OnDisable()
    {
        if (PM.Instance != null)
        {
            PM.Instance.OnSetNewPauseState -= OnPauseChanged;
            PM.Instance.OnSetNewSlowMotionState -= OnSlowChanged;
        }
    }

    private void Update()
    {
        var pm = PM.Instance;
        if (pm == null) return;

        bool paused = pm.programPaused;
        bool slow   = pm.slowMotionActive;

        if (lastPaused != paused || lastSlow != slow)
            SyncFromPMState();
    }

    // --- Public API for UI ---

    public void SetMode(int modeIndex)
    {
        if (modeIndex < 0) modeIndex = 0;
        if (modeIndex > 2) modeIndex = 2;
        SetMode((Mode)modeIndex);
    }

    public void SetMode(Mode mode)
    {
        SetModeInternal(mode, invokePM: true);
    }

    // --- Internal helpers ---

    private void SyncFromPMState()
    {
        var pm = PM.Instance;
        if (pm == null) return;

        bool paused = pm.programPaused;
        bool slow   = pm.slowMotionActive;

        // Remember last observed state to avoid redundant work
        lastPaused = paused;
        lastSlow   = slow;

        // Priority: Pause > Slow > Fast
        Mode resolved =
            paused ? Mode.Pause :
            slow   ? Mode.Slow  :
                     Mode.Fast;

        // Update UI
        SetModeInternal(resolved, invokePM: false);
    }

    private void SetModeInternal(Mode mode, bool invokePM)
    {
        currentMode = mode;

        if (invokePM && PM.Instance != null)
        {
            switch (mode)
            {
                case Mode.Pause:
                    PM.Instance.TriggerSetPauseState(true);
                    PM.Instance.TriggerSetSlowMotionState(false);
                    lastPaused = true;
                    lastSlow   = false;
                    break;

                case Mode.Slow:
                    PM.Instance.TriggerSetPauseState(false);
                    PM.Instance.TriggerSetSlowMotionState(true);
                    lastPaused = false;
                    lastSlow   = true;
                    break;

                default: // Fast
                    PM.Instance.TriggerSetPauseState(false);
                    PM.Instance.TriggerSetSlowMotionState(false);
                    lastPaused = false;
                    lastSlow   = false;
                    break;
            }
        }

        UpdateUIFromMode();
    }

    private void UpdateUIFromMode()
    {
        if (pauseUI) pauseUI.SetModeA(currentMode != Mode.Pause);
        if (slowUI)  slowUI.SetModeA(currentMode != Mode.Slow);
        if (fastUI)  fastUI.SetModeA(currentMode != Mode.Fast);
    }

    // --- Event handlers from ProgramManager ---

    private void OnPauseChanged(bool paused)
    {
        // Update last-observed and recompute UI without re-invoking PM
        lastPaused = paused;
        SyncFromPMState();
    }

    private void OnSlowChanged(bool slow)
    {
        // Update last-observed and recompute UI without re-invoking PM
        lastSlow = slow;
        SyncFromPMState();
    }
}