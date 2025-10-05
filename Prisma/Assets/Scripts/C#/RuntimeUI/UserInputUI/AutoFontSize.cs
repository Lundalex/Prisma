using UnityEngine;
using TMPro;

public class AutoFontSize : MonoBehaviour
{
    // Inspector settings (unchanged behavior)
    [SerializeField] private bool autoAdjustSize = true;
    [SerializeField] private TMP_Text autoText;
    [SerializeField] private TMP_Text referenceText;
    [SerializeField] private float minFontSize = 16f;
    [SerializeField] private int maxSteps = 20;

    // Internal
    private const float THROTTLE_INTERVAL = 0.1f;
    private float step = 1f;

    // State
    private bool _isAdjusting;
    private float _maxFontSize;

    // Throttle state
    private float _nextAllowedTime;
    private bool _adjustScheduled;
    private Coroutine _throttleCo;

    void OnEnable()
    {
        if (!autoText || !referenceText) return;

        _maxFontSize = referenceText.fontSize;
        autoText.fontSize = _maxFontSize;

        if (autoAdjustSize)
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            RequestAdjust();
        }
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        _isAdjusting = false;

        if (_throttleCo != null)
        {
            StopCoroutine(_throttleCo);
            _throttleCo = null;
        }
        _adjustScheduled = false;
    }

    void OnTextChanged(Object obj)
    {
        if (obj == autoText && autoAdjustSize)
            RequestAdjust();
    }

    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && autoAdjustSize)
            RequestAdjust();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (autoText && referenceText)
        {
            _maxFontSize = referenceText.fontSize;
            if (!Application.isPlaying)
                autoText.fontSize = _maxFontSize;
        }
    }
#endif

    // -------- Throttling --------
    void RequestAdjust()
    {
        if (Time.unscaledTime >= _nextAllowedTime && !_isAdjusting)
        {
            _nextAllowedTime = Time.unscaledTime + THROTTLE_INTERVAL;
            AdjustFontSizeToFit();
        }
        else if (!_adjustScheduled)
        {
            _adjustScheduled = true;
            if (_throttleCo != null) StopCoroutine(_throttleCo);
            _throttleCo = StartCoroutine(AdjustWhenAllowed());
        }
    }

    System.Collections.IEnumerator AdjustWhenAllowed()
    {
        while (_adjustScheduled)
        {
            float wait = Mathf.Max(0f, _nextAllowedTime - Time.unscaledTime);
            _adjustScheduled = false;
            if (wait > 0f)
                yield return new WaitForSecondsRealtime(wait);

            _nextAllowedTime = Time.unscaledTime + THROTTLE_INTERVAL;
            AdjustFontSizeToFit();
        }
        _throttleCo = null;
    }

    // -------- Original behavior (shrink when overflow, then grow back up) --------
    void AdjustFontSizeToFit()
    {
        if (_isAdjusting || !autoText) return;
        _isAdjusting = true;

        // Prevent re-entrancy while tweaking size
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        int steps = 0;

        // 1) Shrink while overflowing
        while (IsOverflowing() && autoText.fontSize > minFontSize && steps++ < maxSteps)
        {
            autoText.fontSize = Mathf.Max(minFontSize, autoText.fontSize - step);
            autoText.ForceMeshUpdate();
        }

        // 2) Grow back up while there's room (but never exceed _maxFontSize)
        while (!IsOverflowing() && autoText.fontSize + step <= _maxFontSize && steps++ < maxSteps)
        {
            autoText.fontSize += step;
            autoText.ForceMeshUpdate();

            if (IsOverflowing())
            {
                autoText.fontSize -= step; // back off if we just crossed the limit
                autoText.ForceMeshUpdate();
                break;
            }
        }

        // Re-subscribe for future changes
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);

        _isAdjusting = false;
    }

    bool IsOverflowing()
    {
        autoText.ForceMeshUpdate();
        var rect = autoText.rectTransform.rect;
        Vector2 preferred = autoText.GetPreferredValues(autoText.text, rect.width, Mathf.Infinity);

        const float epsilon = 0.01f;
        return preferred.y > rect.height + epsilon || preferred.x > rect.width + epsilon;
    }
}