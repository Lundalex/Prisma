using System.Collections;   
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Michsky.MUIP;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class UserAnswerField : MonoBehaviour
{
    [Header("Answer Settings")]
    public string answerKey;
    public bool caseSensitive = false;

    [Header("Colors")]
    [SerializeField, ColorUsage(true, true)] private Color defaultColor = Color.gray;
    [SerializeField, ColorUsage(true, true)] private Color editColor = Color.blue;
    [SerializeField, ColorUsage(true, true)] private Color successColor = Color.green;
    [SerializeField, ColorUsage(true, true)] private Color failColor = Color.red;

    [Header("Outline (UI)")]
    [SerializeField] private Image outlineImage;
    [SerializeField] private GameObject outlineObject;

    [Header("Correct Mark (UI)")]
    [SerializeField] private RectTransform correctMark;

    [Header("Input")]
    [SerializeField] private TMP_InputField inputField;

    // ─────────────────────────────────────────────────────────────────────────────
    [Header("Window Manager")]
    [SerializeField] private WindowManager submitNextWindowManager;
    [SerializeField] private string submitWindowName;
    [SerializeField] private string nextWindowName;

    [Header("Fail Feedback")]
    [Tooltip("Shake amplitude in UI pixels (±X).")]
    [SerializeField] private float shakePixels = 8f;
    [Tooltip("Duration of a single shake cycle.")]
    [SerializeField] private float shakeCycleDuration = 0.14f;
    [Tooltip("How many shake cycles to play.")]
    [SerializeField] private int shakeCycles = 3;

    [Tooltip("Time to lerp to fail color.")]
    [SerializeField] private float outlineLerp = 0.06f;

    [SerializeField] private RectTransform _rt;

    // ---------- Success Feedback (runtime + editor preview) ----------
    [Header("Success Feedback")]
    [Tooltip("Duration of the correct-mark animation (seconds).")]
    [SerializeField] private float correctMarkDuration = 0.6f;

    [Tooltip("Total number of full spins (1 = 360°).")]
    [SerializeField] private float correctMarkSpins = 1f;

    [Tooltip("Uniform start scale relative to the mark's base scale.")]
    [SerializeField] private float correctMarkScaleStart = 0.9f;

    [Tooltip("Uniform end scale relative to the mark's base scale.")]
    [SerializeField] private float correctMarkScaleEnd = 1.2f;

    [Tooltip("Curve mapping 0..1 time → scale progress between Start and End.")]
    [SerializeField] private AnimationCurve correctScaleCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Curve mapping 0..1 time → rotation amount (0..1 of total spins).")]
    [SerializeField] private AnimationCurve correctRotationCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

#if UNITY_EDITOR
    [Header("Preview (Edit Mode)")]
    [Tooltip("Click to preview the correct-mark animation in Edit Mode. Resets to false automatically.")]
    [SerializeField] private bool previewCorrectMark = false;
    bool _previewPlaying = false;
    double _previewStartTime = 0.0;
    bool _previewWasActive = false;
#endif
    // ----------------------------------------------------------------

    enum Verdict { None, Success, Fail }

    Color _outlineBaseColor;
    Coroutine _shakeCo, _flashCo;

    // Success mark state
    Coroutine _correctMarkCo;
    Vector3 _markBaseScale = Vector3.one;
    float _markBaseZ = 0f;
    bool _playedCorrectMark = false; // runtime-only replay guard

    bool _editingOverride;

    Verdict _verdict = Verdict.None;
    string _lastSubmittedText = "";
    bool _dirtySinceSubmit = false;

    bool _prevEditing = false;
    bool _lostFocusAfterSubmit = false;

    void Awake()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (outlineImage != null)
        {
            _outlineBaseColor = defaultColor;
            outlineImage.color = defaultColor;
        }

        if (correctMark != null)
        {
            _markBaseScale = correctMark.localScale;
            _markBaseZ = correctMark.localEulerAngles.z;

            // Start inactive only in runtime
            if (Application.isPlaying)
                correctMark.gameObject.SetActive(false);
        }

        // >>> Open submitWindowName at startup
        if (submitNextWindowManager != null)
        {
            submitNextWindowManager.OpenWindow(submitWindowName);
        }
    }

    void OnDisable()
    {
        if (_shakeCo != null) StopCoroutine(_shakeCo);
        if (_flashCo != null) StopCoroutine(_flashCo);
        if (_correctMarkCo != null) StopCoroutine(_correctMarkCo);

        if (_rt != null) _rt.anchoredPosition = Vector2.zero;
        if (outlineImage != null) outlineImage.color = _outlineBaseColor;

        _editingOverride = false;
        _dirtySinceSubmit = false;
        _prevEditing = false;
        _lostFocusAfterSubmit = false;
    }

    void Update()
    {
#if UNITY_EDITOR
        // Handle Edit-Mode preview first; then bail before runtime logic
        if (!Application.isPlaying)
        {
            EditorPreviewTick();
            return;
        }
#endif

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
        else if (_verdict == Verdict.Success)
        {
            if (outlineImage.color != successColor) outlineImage.color = successColor;
        }
        else // Fail
        {
            if (!_prevEditing && editing && _lostFocusAfterSubmit)
            {
                outlineImage.color = editColor;
                _editingOverride = true;
            }

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

        if (_verdict != Verdict.None)
        {
            if (_prevEditing && !editing) _lostFocusAfterSubmit = true;
        }

        _prevEditing = editing;
    }

    public void ProcessAnswer()
    {
        string answer = inputField.text;
        bool answerIsCorrect = CompareWithAnswerKey(answer, answerKey);

        outlineObject.SetActive(true);

        if (answerIsCorrect)
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            outlineImage.color = successColor;
            _verdict = Verdict.Success;
            _lastSubmittedText = answer;
            _dirtySinceSubmit = false;
            _lostFocusAfterSubmit = false;

            ShowCorrectMarkOnce(); // also switches window to "Next"
        }
        else
        {
            if (_flashCo != null) StopCoroutine(_flashCo);
            _flashCo = StartCoroutine(FlashOutline(failColor));

            if (_shakeCo != null) StopCoroutine(_shakeCo);
            _shakeCo = StartCoroutine(ShakeObject());

            _verdict = Verdict.Fail;
            _lastSubmittedText = answer;
            _dirtySinceSubmit = false;
            _lostFocusAfterSubmit = false;
        }
    }

    protected virtual bool CompareWithAnswerKey(string answer, string answerKey)
    {
        if (caseSensitive) return answer == answerKey;
        return string.Equals(answer, answerKey, System.StringComparison.OrdinalIgnoreCase);
    }

    IEnumerator FlashOutline(Color target)
    {
        if (outlineImage == null) yield break;

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
                if (f < 0.25f) x = Mathf.Lerp(0f, +shakePixels, f / 0.25f);
                else if (f < 0.75f) x = Mathf.Lerp(+shakePixels, -shakePixels, (f - 0.25f) / 0.5f);
                else x = Mathf.Lerp(-shakePixels, 0f, (f - 0.75f) / 0.25f);

                _rt.anchoredPosition = basePos + new Vector2(x, 0f);
                yield return null;
            }
        }

        _rt.anchoredPosition = basePos;
        _shakeCo = null;
    }

    // ---------- Correct Mark animation (shared) ----------

    void ShowCorrectMarkOnce()
    {
        if (correctMark == null) return;
        if (_playedCorrectMark) return;

        _playedCorrectMark = true;
        correctMark.gameObject.SetActive(true);

        // >>> Switch to nextWindowName at the same moment the correct mark is activated
        if (submitNextWindowManager != null)
        {
            submitNextWindowManager.OpenWindow(nextWindowName);
        }

        if (_correctMarkCo != null) StopCoroutine(_correctMarkCo);
        _correctMarkCo = StartCoroutine(AnimateCorrectMark());
    }

    IEnumerator AnimateCorrectMark()
    {
        float d = Mathf.Max(0.0001f, correctMarkDuration);
        float t = 0f;

        while (t < d)
        {
            t += Time.unscaledDeltaTime;
            float f = Mathf.Clamp01(t / d);
            EvaluateCorrectMark(f);
            yield return null;
        }

        _correctMarkCo = null;
    }

    void EvaluateCorrectMark(float f01)
    {
        if (correctMark == null) return;

        float sT = (correctScaleCurve != null) ? correctScaleCurve.Evaluate(f01) : f01;
        float rT = (correctRotationCurve != null) ? correctRotationCurve.Evaluate(f01) : f01;

        // Scale from Start -> End (relative to base)
        float uniformScale = Mathf.Lerp(correctMarkScaleStart, correctMarkScaleEnd, sT);
        uniformScale = Mathf.Max(0.0001f, uniformScale);
        correctMark.localScale = Vector3.Scale(_markBaseScale, Vector3.one * uniformScale);

        // Rotation
        float angle = rT * correctMarkSpins * 360f;
        correctMark.localRotation = Quaternion.Euler(0f, 0f, _markBaseZ + angle);
    }

#if UNITY_EDITOR
    // ---------- Edit Mode preview ----------
    void EditorPreviewTick()
    {
        if (correctMark == null) return;

        // Start preview when the bool is toggled on
        if (previewCorrectMark && !_previewPlaying)
        {
            _previewPlaying = true;
            _previewStartTime = EditorApplication.timeSinceStartup;

            // Cache current active state and ensure visible
            _previewWasActive = correctMark.gameObject.activeSelf;
            correctMark.gameObject.SetActive(true);

            // Ensure we start from base
            correctMark.localScale = _markBaseScale;
            correctMark.localRotation = Quaternion.Euler(0f, 0f, _markBaseZ);

            // Force a repaint so you see the change immediately
            SceneView.RepaintAll();
        }

        if (_previewPlaying)
        {
            double elapsed = EditorApplication.timeSinceStartup - _previewStartTime;
            float d = Mathf.Max(0.0001f, correctMarkDuration);

            // Allow f up to 3 and stop at >= 3 (as per your previous behaviour)
            float f = Mathf.Clamp((float)(elapsed / d), 0, 3f);

            EvaluateCorrectMark(f);

            SceneView.RepaintAll();

            if (f >= 3f)
            {
                _previewPlaying = false;
                previewCorrectMark = false;

                // Restore base transform and active state
                correctMark.localScale = _markBaseScale;
                correctMark.localRotation = Quaternion.Euler(0f, 0f, _markBaseZ);
                correctMark.gameObject.SetActive(_previewWasActive);

                SceneView.RepaintAll();
            }
        }
    }
#endif
}