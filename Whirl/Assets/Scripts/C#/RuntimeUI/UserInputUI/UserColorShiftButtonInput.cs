using System.Collections;
using System.Collections.Generic;
using Michsky.MUIP;
using UnityEngine;

public class UserColorShiftButtonInput : UserButtonInput
{
    [Header("Color Shift")]
    [Range(0.5f, 10.0f), SerializeField] private float colorShiftSpeed = 1.5f;
    [SerializeField] private List<Color> shiftColors = new();

    [Header("Tip Notification")]
    [SerializeField] private float triggerThreshold = 5.0f;
    [SerializeField] private NotificationManager tipNotification;

    // Private
    private bool colorShiftCoroutineStarted = false;
    private bool tipNotificationHasTriggered = false;
    private Timer tipNotificationDelayTimer;
    
    [ContextMenu("Start Color Shift")]
    public void StartColorShift()
    {
        if (!colorShiftCoroutineStarted)
        {
            ResetDelayTimer();
            StartCoroutine(ColorShiftCoroutine());
            colorShiftCoroutineStarted = true;
        }
    }

    [ContextMenu("Stop Color Shift")]
    public void StopColorShift()
    {
        if (colorShiftCoroutineStarted)
        {
            StopCoroutine(ColorShiftCoroutine());
            colorShiftCoroutineStarted = false;
        }
    }

    private void ResetDelayTimer()
    {
        if (tipNotificationDelayTimer == null) tipNotificationDelayTimer = new(triggerThreshold);
        else tipNotificationDelayTimer.Reset();
    }

    private IEnumerator ColorShiftCoroutine()
    {
        Timer timer = new(0);
        int colorCount = shiftColors.Count;
        if (colorCount < 2)
        {
            Debug.LogWarning("Cannot color shift - too few shift colors. UserButtonInput: " + this.name);
            yield break;
        }
        while (true)
        {
            colorCount = shiftColors.Count;

            float t0 = timer.GetTime() * colorShiftSpeed;
            float t = Mathf.Repeat(t0, 1.0f);

            float colorIndexFloat = t * colorCount;
            int colorIndex = Mathf.FloorToInt(colorIndexFloat) % colorCount;
            int nextColorIndex = (colorIndex + 1) % colorCount;
            float frac = colorIndexFloat - colorIndex;

            Color colStart = shiftColors[colorIndex];
            Color colEnd = shiftColors[nextColorIndex];

            primaryColor = Color.Lerp(colStart, colEnd, frac);
            containerTrimImage.color = primaryColor;

            HandleTipNotification();

            yield return new WaitForSeconds(0.01f);
        }
    }

    private void HandleTipNotification()
    {
        if (tipNotificationHasTriggered || tipNotification == null) return;
        if (!tipNotification.isActiveAndEnabled) return;

        if (tipNotificationDelayTimer.Check())
        {
            tipNotification.OpenNotification();
            tipNotificationHasTriggered = true;
        }
    }

    private void OnDestroy() => StopColorShift();
}