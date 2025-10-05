using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using System.Collections;

public class MaskedProgressBar : MonoBehaviour
{
    [SerializeField, Range(0.0f, 1.0f)]  private float initialProgress = 0f;

    [SerializeField]
    private UnityEvent onProgressComplete;

    public RectTransform parentRect;
    public RectTransform thisRect;
    public RectMask2D rectMask2D;

    private float progress;

    private void OnValidate() => Progress = initialProgress;
    private void Start() => Progress = initialProgress;

    public float Progress
    {
        get => progress;
        set
        {
            progress = Mathf.Clamp01(value);
            UpdateMask();
            if (progress >= 1f) onProgressComplete.Invoke();
        }
    }

    public void StartTimer(float totalTime)
    {
        StopAllCoroutines();
        StartCoroutine(TimerCoroutine(totalTime));
    }

    private IEnumerator TimerCoroutine(float totalTime)
    {
        float elapsed = 0f;
        while (elapsed < totalTime)
        {
            elapsed += Time.deltaTime;
            Progress = elapsed / totalTime;
            yield return null;
        }

        Progress = 1f;
    }

    private void UpdateMask()
    {
        float totalWidth = thisRect.sizeDelta.x * parentRect.localScale.x;
        rectMask2D.padding = new Vector4(0, 0, progress * totalWidth, 0);
    }
}