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
    }

    private void Start()
    {
        if (PM.Instance != null && PM.Instance.pauseOnStart)
            SetModeInternal(Mode.Pause, invokePM: false);
        else
            SetModeInternal(Mode.Fast, invokePM: false);
    }

    private void OnDisable()
    {
        if (PM.Instance != null)
        {
            PM.Instance.OnSetNewPauseState -= OnPauseChanged;
            PM.Instance.OnSetNewSlowMotionState -= OnSlowChanged;
        }
    }

    // --- Public API for UI ---

    public void SetMode(int modeIndex)
    {
        // Clamp to valid values then set
        if (modeIndex < 0) modeIndex = 0;
        if (modeIndex > 2) modeIndex = 2;
        SetMode((Mode)modeIndex);
    }

    public void SetMode(Mode mode)
    {
        SetModeInternal(mode, invokePM: true);
    }

    // --- Internal helpers ---

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
                    lastSlow = false;
                    break;

                case Mode.Slow:
                    PM.Instance.TriggerSetPauseState(false);
                    PM.Instance.TriggerSetSlowMotionState(true);
                    lastPaused = false;
                    lastSlow = true;
                    break;

                default: // Fast
                    PM.Instance.TriggerSetPauseState(false);
                    PM.Instance.TriggerSetSlowMotionState(false);
                    lastPaused = false;
                    lastSlow = false;
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

    // --- Event handlers from the existing separate system ---

    private void OnPauseChanged(bool paused)
    {
        RecomputeFromSignals(paused: paused, slow: null);
    }

    private void OnSlowChanged(bool slow)
    {
        RecomputeFromSignals(paused: null, slow: slow);
    }

    private void RecomputeFromSignals(bool? paused, bool? slow)
    {
        if (paused.HasValue) lastPaused = paused.Value;
        if (slow.HasValue)   lastSlow   = slow.Value;

        if (!lastPaused.HasValue && !lastSlow.HasValue)
            return;

        // Priority: Pause > Slow > Fast
        Mode resolved =
            (lastPaused == true) ? Mode.Pause :
            (lastSlow   == true) ? Mode.Slow  :
                                   Mode.Fast;

        SetModeInternal(resolved, invokePM: false);
    }
}