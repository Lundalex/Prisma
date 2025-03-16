using UnityEngine;
using PM = ProgramManager;

public class Timer
{
    private float time;
    private readonly float threshold;
    private readonly TimeType timeType;
    private readonly bool resetTimerOnThresholdReached;

    /// <summary>A timer which is automatically subscribed to the program update life cycle</summary>
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
            // Reset/subtract from the accumulated time
            if (resetIfThresholdReached)
            {
                if (resetTimerOnThresholdReached) time = 0;
                else time -= threshold;
            }

            return true;
        }
        else return false;
    }

    /// <summary>Get the elapsed time since starting/resetting the timer</summary>
    public float GetTime() => time;

    /// <summary>Reset the accumulated time</summary>
    public void Reset() => time = 0;
}