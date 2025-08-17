using UnityEngine;
using TMPro;

public class AutoFontSize : MonoBehaviour
{
    // Inspctor settings
    [SerializeField] private bool autoAdjustSize = true;
    [SerializeField] private TMP_Text autoText;
    [SerializeField] private TMP_Text referenceText;
    [SerializeField] private float minFontSize = 16f;
    [SerializeField] private int maxSteps = 20;

    // Private settings
    private float step = 1f; // size change per step

    // State variables
    bool _isAdjusting;
    float _maxFontSize; // original target (from reference)

    void OnEnable()
    {
        if (!autoText || !referenceText) return;

        _maxFontSize = referenceText.fontSize;
        autoText.fontSize = _maxFontSize;

        if (autoAdjustSize)
        {
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
            AdjustFontSizeToFit();
        }
    }

    void OnDisable()
    {
        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);
        _isAdjusting = false;
    }

    void OnTextChanged(Object obj)
    {
        if (obj == autoText && autoAdjustSize)
            AdjustFontSizeToFit();
    }

    void OnRectTransformDimensionsChange()
    {
        if (isActiveAndEnabled && autoAdjustSize)
            AdjustFontSizeToFit();
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

    void AdjustFontSizeToFit()
    {
        if (_isAdjusting || !autoText) return;
        _isAdjusting = true;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        int steps = 0;

        // 1) Shrink while overflowing
        while (IsOverflowing() && autoText.fontSize > minFontSize && steps++ < maxSteps)
        {
            autoText.fontSize = Mathf.Max(minFontSize, autoText.fontSize - step);
            autoText.ForceMeshUpdate();
        }

        // 2) Grow back up while there's room (but never exceed _maxFontSize)
        //    We grow until we would overflow, then back off one step.
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

        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        _isAdjusting = false;
    }

    bool IsOverflowing()
    {
        // Compare preferred size at current font size to the rect
        autoText.ForceMeshUpdate();
        var rect = autoText.rectTransform.rect;

        Vector2 preferred = autoText.GetPreferredValues(autoText.text, rect.width, Mathf.Infinity);

        const float epsilon = 0.01f;
        return preferred.y > rect.height + epsilon || preferred.x > rect.width + epsilon;
    }
}