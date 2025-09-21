using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[RequireComponent(typeof(RectTransform))]
public class FixedRectPosition : MonoBehaviour
{
    public float x = 0f;
    public float y = 0f;
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

        Vector2 current = _rt.anchoredPosition;

        bool xDiff = Mathf.Abs(current.x - x) > tolerance;
        bool yDiff = Mathf.Abs(current.y - y) > tolerance;

        if (xDiff || yDiff)
        {
            _rt.anchoredPosition = new Vector2(
                xDiff ? x : current.x,
                yDiff ? y : current.y
            );
        }
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying)
        {
            _rt = GetComponent<RectTransform>();
            DeferEditorApply(); // don't set position directly during OnValidate
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
