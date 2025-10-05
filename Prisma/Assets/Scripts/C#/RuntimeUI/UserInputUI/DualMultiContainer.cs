using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DualMultiContainer : MultiContainer
{
    [Header("Alt Stretch Targets")]
    [SerializeField] public RectTransform[] altStretchTargets;

    [Header("Alt Stretch Target Offsets")]
    [SerializeField] private float altLeftOffset = 0f;
    [SerializeField] private float altRightOffset = 0f;
    [SerializeField] private float altTopOffset = 0f;
    [SerializeField] private float altBottomOffset = 0f;

    [SerializeField] private bool useAltStretchTarget = false;

    public void SetStretchTargetAlt(bool useAlt)
    {
        useAltStretchTarget = useAlt;
        MatchAnchorsToOuterGlobal();
    }

    protected override void MatchAnchorsToOuterGlobal()
    {
        // In Play Mode: only update the selected set (preserves old behavior)
        if (Application.isPlaying)
        {
            if (useAltStretchTarget)
                UpdateAltTargets();
            else
                base.MatchAnchorsToOuterGlobal();

            return;
        }

        // In Edit Mode (Inspector changes): update BOTH sets
        base.MatchAnchorsToOuterGlobal();
        UpdateAltTargets();
    }

    private void UpdateAltTargets()
    {
        if (altStretchTargets == null) return;

        for (int i = 0; i < altStretchTargets.Length; i++)
        {
            var t = altStretchTargets[i];
            if (t == null) continue;
            UpdateAnchorForTarget(t, altLeftOffset, altRightOffset, altTopOffset, altBottomOffset);
        }
    }

    private void OnEnable()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
            EditorUpdateNow();
#endif
    }

#if UNITY_EDITOR
    private bool _deferred;

    private void OnValidate()
    {
        if (Application.isPlaying) return;

        if (!_deferred)
        {
            _deferred = true;
            EditorApplication.delayCall += EditorApplyDeferred;
        }
    }

    private void EditorApplyDeferred()
    {
        _deferred = false;

        if (this == null) return;
        if (PrefabUtility.IsPartOfPrefabAsset(gameObject)) return;

        EditorUpdateNow();
    }

    private void EditorUpdateNow()
    {
        MatchAnchorsToOuterGlobal();
    }
#endif
}