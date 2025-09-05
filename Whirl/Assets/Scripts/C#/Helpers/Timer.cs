using System;
using UnityEngine;
using PM = ProgramManager;

public class Timer : IDisposable
{
    private float time;
    private readonly float threshold;
    private readonly TimeType timeType;
    private readonly bool resetTimerOnThresholdReached;

    private bool _isDisposed;

    /// <summary>
    /// A timer which is automatically subscribed to the program update life cycle.
    /// IMPORTANT: Call Dispose() when you're done with the timer (or clear ProgramManager.OnProgramUpdate) to avoid leaks.
    /// </summary>
    public Timer(float threshold, TimeType timeType = TimeType.Clamped, bool resetTimerOnThresholdReached = true, float time = 0)
    {
        this.threshold = threshold;
        this.timeType = timeType;
        this.resetTimerOnThresholdReached = resetTimerOnThresholdReached;
        this.time = time;

        PM.Instance.OnProgramUpdate += IncrementTime;
    }

    private void IncrementTime(bool doUpdateClampedTime)
    {
        if (!doUpdateClampedTime && timeType != TimeType.NonClamped) return;
        switch (timeType)
        {
            case TimeType.Clamped:
                time += PM.Instance.clampedDeltaTime;
                break;
            case TimeType.NonClamped:
                time += Time.deltaTime;
                break;
            case TimeType.Scaled:
                time += PM.Instance.scaledDeltaTime;
                break;
            default:
                Debug.Log("TimeType not recognised. See class 'Timer'");
                break;
        }
    }

    /// <summary>Check whether the internal accumulated time has exceeded the threshold</summary>
    public bool Check(bool resetIfThresholdReached = true)
    {
        if (time >= threshold)
        {
            if (resetIfThresholdReached)
            {
                if (resetTimerOnThresholdReached) time = 0;
                else time -= threshold;
            }
            return true;
        }
        return false;
    }
    
    public float GetTime() => time;
    public void Reset() => time = 0;
    public void Dispose()
    {
        if (_isDisposed) return;
        _isDisposed = true;

        var pm = PM.Instance;
        if (pm != null) pm.OnProgramUpdate -= IncrementTime;
    }
}