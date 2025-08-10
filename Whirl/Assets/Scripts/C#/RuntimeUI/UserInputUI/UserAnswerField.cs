using System.Collections; 
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UserAnswerField : MonoBehaviour
{
    [Header("Answer Settings")]
    [SerializeField] private string answerKey;
    [SerializeField] private bool caseSensitive = false;

    [Header("Colors")]
    [SerializeField, ColorUsage(true, true)] private Color defaultColor = Color.gray;
    [SerializeField, ColorUsage(true, true)] private Color editColor = Color.blue;
    [SerializeField, ColorUsage(true, true)] private Color successColor = Color.green;
    [SerializeField, ColorUsage(true, true)] private Color failColor = Color.red;

    [Header("Outline (UI)")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject outlineObject;

    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;

    [Header("Fail Feedback")]
    [Tooltip("Shake amplitude in UI pixels (±X).")]
    [SerializeField] private float shakePixels = 8f;
    [Tooltip("Duration of a single shake cycle.")]
    [SerializeField] private float shakeCycleDuration = 0.14f;
    [Tooltip("How many shake cycles to play.")]
    [SerializeField] private int shakeCycles = 3;

    [Tooltip("Time to lerp to fail color.")]
    [SerializeField] private float outlineLerp = 0.06f;

    enum Verdict { None, Success, Fail }

    Color _outlineBaseColor;
    RectTransform _rt;
    Coroutine _shakeCo, _flashCo;
    bool _editingOverride;

    Verdict _verdict = Verdict.None;
    string _lastSubmittedText = "";
    bool _dirtySinceSubmit = false;

    void Awake()
    {
        _rt = GetComponent<RectTransform>();
        if (outlineImage != null)
        {
            _outlineBaseColor = defaultColor;
            outlineImage.color = defaultColor;
        }
    }

    void OnDisable()
    {
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        if (_flashCo != null) StopCoroutine(_flashCo);
        if (_rt != null) _rt.anchoredPosition = Vector2.zero;
        if (outlineImage != null) outlineImage.color = _outlineBaseColor;
        _editingOverride = false;
        _dirtySinceSubmit = false;
    }

    void Update()
    {
        if (outlineImage == null) return;

        bool editing = TMPInputChecker.UserIsUsingInputField(inputField);

        if (_verdict == Verdict.None)
        {
            if (editing)
            {
                if (!_editingOverride)
                {
                    if (_flashCo != null) { StopCoroutine(_flashCo); _flashCo = null; }
                }
                outlineObject.SetActive(true);
                outlineImage.color = editColor;
                _editingOverride = true;
            }
            else
            {
                if (_editingOverride || outlineImage.color == editColor)
                {
                    outlineImage.color = defaultColor;
                    _editingOverride = false;
                }
            }
        }
        else
        {
            if (editing && !_dirtySinceSubmit && inputField.text != _lastSubmittedText)
            {
                outlineImage.color = editColor;
                _dirtySinceSubmit = true;
                _editingOverride = true;
            }
            else if (!editing && outlineImage.color == editColor)
            {
                outlineImage.color = defaultColor;
                _editingOverride = false;
            }
        }
    }

    public void ProcessAnswer()
    {
        string answer = inputField.text;
        bool answerIsCorrect = CompareWithAnswerKey(answer, answerKey);

        outlineObject.SetActive(true);

        if (answerIsCorrect)
        {
            // immediate success color (no shake)
            if (_flashCo != null) StopCoroutine(_flashCo);
            outlineImage.color = successColor;
            _verdict = Verdict.Success;
            _lastSubmittedText = answer;
            _dirtySinceSubmit = false;
        }
        else
        {
            // flash to fail color + shake like CSS
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashOutline(failColor));

            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeObject());

            _verdict = Verdict.Fail;
            _lastSubmittedText = answer;
            _dirtySinceSubmit = false;
        }
    }

    private bool CompareWithAnswerKey(string answer, string answerKey)
    {
        if (caseSensitive) return answer == answerKey;
        return string.Equals(answer, answerKey, System.StringComparison.OrdinalIgnoreCase);
    }

    IEnumerator FlashOutline(Color target)
    {
        if (outlineImage == null) yield break;

        // Lerp to fail color
        float t = 0f;
        Color from = outlineImage.color;
        while (t < outlineLerp)
        {
            t += Time.unscaledDeltaTime;
            outlineImage.color = Color.Lerp(from, target, t / Mathf.Max(0.0001f, outlineLerp));
            yield return null;
        }

        outlineImage.color = target;
        _flashCo = null;
    }

    IEnumerator ShakeObject()
    {
        if (_rt == null) yield break;

        Vector2 basePos = _rt.anchoredPosition;

        for (int i = 0; i < shakeCycles; i++)
        {
            float d = Mathf.Max(0.0001f, shakeCycleDuration);
            float t = 0f;

            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                float f = Mathf.Clamp01(t / d);

                float x;
                if (f < 0.25f)          x = Mathf.Lerp(0f,  +shakePixels, f / 0.25f);
                else if (f < 0.75f)     x = Mathf.Lerp(+shakePixels, -shakePixels, (f - 0.25f) / 0.5f);
                else                    x = Mathf.Lerp(-shakePixels, 0f, (f - 0.75f) / 0.25f);

                _rt.anchoredPosition = basePos + new Vector2(x, 0f);
                yield return null;
            }
        }

        _rt.anchoredPosition = basePos;
        _shakeCo = null;
    }
}