using UnityEngine;
using TMPro;

public class AutoFontSize : MonoBehaviour
{
    [SerializeField] private bool autoAdjustSize;
    [SerializeField] private TMP_Text autoText;
    [SerializeField] private TMP_Text referenceText;
    [SerializeField] private float minFontSize = 16f;
    [SerializeField] private int maxSteps = 20;

    bool _isAdjusting;

    void OnEnable()
    {
        autoText.fontSize = referenceText.fontSize;
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

    void AdjustFontSizeToFit()
    {
        if (_isAdjusting) return;
        _isAdjusting = true;

        TMPro_EventManager.TEXT_CHANGED_EVENT.Remove(OnTextChanged);

        int steps = 0;
        while (IsTruncated() && autoText.fontSize > minFontSize && steps++ < maxSteps)
        {
            autoText.fontSize -= 1f;
            autoText.ForceMeshUpdate();
        }

        TMPro_EventManager.TEXT_CHANGED_EVENT.Add(OnTextChanged);
        _isAdjusting = false;
    }

    bool IsTruncated()
    {
        autoText.ForceMeshUpdate();
        string displayed = autoText.GetParsedText();
        // Detect Unicode ellipsis or three periods
        return displayed.EndsWith("…");
    }
}