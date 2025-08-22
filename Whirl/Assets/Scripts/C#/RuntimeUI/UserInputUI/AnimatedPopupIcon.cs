using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class AnimatedPopupIcon : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RectTransform icon;

    [Header("Initial Values (absolute)")]
    [Tooltip("Multiplier applied to icon's base X scale at t=0 of the first stage.")]
    [SerializeField] private float startScaleX = 1f;
    [Tooltip("Multiplier applied to icon's base Y scale at t=0 of the first stage.")]
    [SerializeField] private float startScaleY = 1f;
    [Tooltip("Rotation (degrees, around Z) relative to icon's base rotation at t=0 of the first stage.")]
    [SerializeField] private float startRotation = 0f;

    [System.Serializable]
    public class Stage
    {
        [Min(0.0001f)] public float duration = 0.2f;

        [Header("Normalized Curves (0..1 ➜ 0..1)")]
        [Tooltip("Interpolates from previous stage's ScaleX value to this stage's To ScaleX.")]
        public AnimationCurve scaleX = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Interpolates from previous stage's ScaleY value to this stage's To ScaleY.")]
        public AnimationCurve scaleY = AnimationCurve.Linear(0, 0, 1, 1);
        [Tooltip("Interpolates from previous stage's Rotation (deg) to this stage's To Rotation (deg).")]
        public AnimationCurve rotation = AnimationCurve.Linear(0, 0, 1, 1);

        [Header("Stage End Values (absolute)")]
        [Tooltip("Resulting multiplier for X at the END of this stage.")]
        public float toScaleX = 1f;
        [Tooltip("Resulting multiplier for Y at the END of this stage.")]
        public float toScaleY = 1f;
        [Tooltip("Resulting rotation (deg, around Z) at the END of this stage, relative to icon base.")]
        public float toRotation = 0f;
    }

    [Header("Stages (customize count)")]
    [SerializeField] private List<Stage> stages = new List<Stage>()
    {
        new Stage { duration = 0.18f, scaleX = AnimationCurve.EaseInOut(0,0,1,1), scaleY = AnimationCurve.EaseInOut(0,0,1,1), rotation = AnimationCurve.EaseInOut(0,0,1,1), toScaleX = 1.2f, toScaleY = 1.05f, toRotation = 120f },
        new Stage { duration = 0.24f, scaleX = AnimationCurve.EaseInOut(0,0,1,1), scaleY = AnimationCurve.EaseInOut(0,0,1,1), rotation = AnimationCurve.EaseInOut(0,0,1,1), toScaleX = 1.0f, toScaleY = 1.1f,  toRotation = 280f },
        new Stage { duration = 0.18f, scaleX = AnimationCurve.EaseInOut(0,0,1,1), scaleY = AnimationCurve.EaseInOut(0,0,1,1), rotation = AnimationCurve.EaseInOut(0,0,1,1), toScaleX = 1.0f, toScaleY = 1.0f,  toRotation = 360f },
    };

#if UNITY_EDITOR
    [Header("Preview (Edit Mode)")]
    [Tooltip("Tick to preview the animation in the editor. Resets to false automatically.")]
    [SerializeField] private bool previewInEditor = false;
    bool _previewPlaying;
    double _previewStart;
#endif

    Vector3 _baseScale = Vector3.one;
    float _baseZ = 0f;
    Coroutine _co;

    void Awake()
    {
        if (icon == null) icon = GetComponent<RectTransform>();
        CacheBase();
        if (Application.isPlaying && icon != null) icon.gameObject.SetActive(false); // active only while animating at runtime
    }

    void OnEnable()
    {
        if (icon == null) icon = GetComponent<RectTransform>();
        CacheBase();
    }

    void OnDisable()
    {
        if (_co != null) StopCoroutine(_co);
        ResetIcon();
#if UNITY_EDITOR
        _previewPlaying = false;
        previewInEditor = false;
#endif
    }

    void CacheBase()
    {
        if (icon == null) return;
        _baseScale = icon.localScale;
        _baseZ = icon.localEulerAngles.z;
    }

    void ResetIcon()
    {
        if (icon == null) return;
        icon.localScale = _baseScale;
        icon.localRotation = Quaternion.Euler(0f, 0f, _baseZ);
        icon.gameObject.SetActive(false);
    }

    /// <summary>Start the multi-stage popup animation. Safe to call multiple times.</summary>
    public void Play()
    {
        if (icon == null || stages == null || stages.Count == 0) return;
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(AnimateRuntime());
    }

    IEnumerator AnimateRuntime()
    {
        // Prep
        var fromX = startScaleX;
        var fromY = startScaleY;
        var fromR = startRotation;

        icon.localScale = new Vector3(_baseScale.x * Mathf.Max(0.0001f, fromX),
                                      _baseScale.y * Mathf.Max(0.0001f, fromY),
                                      _baseScale.z);
        icon.localRotation = Quaternion.Euler(0f, 0f, _baseZ + fromR);
        icon.gameObject.SetActive(true);

        // Stages (each uses normalized curves 0..1)
        for (int i = 0; i < stages.Count; i++)
        {
            Stage s = stages[i];
            float t = 0f;
            float dur = Mathf.Max(0.0001f, s.duration);

            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / dur);

                float nx = Eval01(s.scaleX, p);
                float ny = Eval01(s.scaleY, p);
                float nr = Eval01(s.rotation, p);

                float curX = Mathf.Lerp(fromX, s.toScaleX, nx);
                float curY = Mathf.Lerp(fromY, s.toScaleY, ny);
                float curR = Mathf.Lerp(fromR, s.toRotation, nr);

                Apply(curX, curY, curR);
                yield return null;
            }

            fromX = s.toScaleX;
            fromY = s.toScaleY;
            fromR = s.toRotation;
        }

        // Cleanup
        ResetIcon();
        _co = null;
    }

    void Apply(float sx, float sy, float rotDeg)
    {
        sx = Mathf.Max(0.0001f, sx);
        sy = Mathf.Max(0.0001f, sy);

        icon.localScale = new Vector3(_baseScale.x * sx, _baseScale.y * sy, _baseScale.z);
        icon.localRotation = Quaternion.Euler(0f, 0f, _baseZ + rotDeg);
    }

    static float Eval01(AnimationCurve c, float t)
    {
        if (c == null || c.length == 0) return Mathf.Clamp01(t);
        return Mathf.Clamp01(c.Evaluate(Mathf.Clamp01(t)));
    }

#if UNITY_EDITOR
    void Update()
    {
        if (Application.isPlaying) return;
        EditorPreviewTick();
    }

    void EditorPreviewTick()
    {
        if (icon == null) icon = GetComponent<RectTransform>();
        if (icon == null || stages == null || stages.Count == 0) return;

        if (previewInEditor && !_previewPlaying)
        {
            _previewPlaying = true;
            _previewStart = EditorApplication.timeSinceStartup;

            CacheBase();
            icon.localScale = _baseScale;                   // reset to base before preview
            icon.localRotation = Quaternion.Euler(0, 0, _baseZ);
            icon.gameObject.SetActive(true);

            SceneView.RepaintAll();
        }

        if (_previewPlaying)
        {
            double elapsed = EditorApplication.timeSinceStartup - _previewStart;

            // Compute total duration
            float total = 0f;
            for (int i = 0; i < stages.Count; i++) total += Mathf.Max(0.0001f, stages[i].duration);

            if (elapsed >= total)
            {
                ResetIcon();
                _previewPlaying = false;
                previewInEditor = false;
                SceneView.RepaintAll();
                return;
            }

            // Sample along the stage list
            float e = (float)elapsed;

            float fromX = startScaleX;
            float fromY = startScaleY;
            float fromR = startRotation;

            for (int i = 0; i < stages.Count; i++)
            {
                Stage s = stages[i];
                float dur = Mathf.Max(0.0001f, s.duration);

                if (e <= dur)
                {
                    float p = Mathf.Clamp01(e / dur);

                    float curX = Mathf.Lerp(fromX, s.toScaleX, Eval01(s.scaleX, p));
                    float curY = Mathf.Lerp(fromY, s.toScaleY, Eval01(s.scaleY, p));
                    float curR = Mathf.Lerp(fromR, s.toRotation, Eval01(s.rotation, p));

                    Apply(curX, curY, curR);
                    break;
                }
                else
                {
                    // advance to next segment
                    e -= dur;
                    fromX = s.toScaleX;
                    fromY = s.toScaleY;
                    fromR = s.toRotation;
                }
            }

            SceneView.RepaintAll();
        }
    }
#endif
}