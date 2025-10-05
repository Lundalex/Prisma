using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class FixedRectSize : MonoBehaviour
{
    [Min(0f)] public float width = 100f;
    [Min(0f)] public float height = 100f;
    [Min(0f)] public float tolerance = 0.01f;

    RectTransform _rt;

#if UNITY_EDITOR
    double _nextEditorTick;
    bool _pendingDeferredApply;
#endif

    void OnEnable()
    {
        _rt = GetComponent<RectTransform>();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            DeferEditorApply();
            return;
        }
#endif
        ApplyIfNeeded();
    }

    void Update()
    {
        if (_rt == null) _rt = GetComponent<RectTransform>();
        if (_rt == null) return;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            if (EditorApplication.timeSinceStartup < _nextEditorTick) return;
            _nextEditorTick = EditorApplication.timeSinceStartup + 1.0;
            ApplyIfNeeded();
            return;
        }
#endif
        ApplyIfNeeded();
    }

    void ApplyIfNeeded()
    {
        if (_rt == null) return;

        float currentW = _rt.rect.width;
        float currentH = _rt.rect.height;

        bool wDiff = Mathf.Abs(currentW - width) > tolerance;
        bool hDiff = Mathf.Abs(currentH - height) > tolerance;

        if (wDiff) _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (hDiff) _rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _rt = GetComponent<RectTransform>();
            DeferEditorApply(); // don't call SetSize directly during OnValidate
        }
    }

    void DeferEditorApply()
    {
        if (_pendingDeferredApply) return;
        _pendingDeferredApply = true;

        EditorApplication.delayCall += () =>
        {
            _pendingDeferredApply = false;
            if (this == null) return;
            if (!isActiveAndEnabled) return;
            if (_rt == null) _rt = GetComponent<RectTransform>();
            ApplyIfNeeded();
        };
    }
#endif
}