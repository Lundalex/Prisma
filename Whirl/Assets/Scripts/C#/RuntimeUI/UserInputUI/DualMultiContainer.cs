using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DualMultiContainer : MultiContainer
{
    [Header("Alt Stretch Targets")]
    [SerializeField] public RectTransform[] altStretchTargets; // <— alt array

    [Header("Alt Stretch Target Offsets")]
    [SerializeField] private float altLeftOffset = 0f;
    [SerializeField] private float altRightOffset = 0f;
    [SerializeField] private float altTopOffset = 0f;
    [SerializeField] private float altBottomOffset = 0f;

    [SerializeField] private bool useAltStretchTarget = false;

    [Header("Default/Alt Objects")]
    [SerializeField] private GameObject[] defaultObjects;
    [SerializeField] private GameObject[] altObjects;

    public void SetStretchTargetAlt(bool useAlt)
    {
        useAltStretchTarget = useAlt;
        UpdateBucketsActive();
        MatchAnchorsToOuterGlobal();
    }

    protected override void MatchAnchorsToOuterGlobal()
    {
        // Choose which list to update
        if (useAltStretchTarget)
        {
            if (altStretchTargets == null) return;
            for (int i = 0; i < altStretchTargets.Length; i++)
            {
                var t = altStretchTargets[i];
                if (t == null) continue;
                UpdateAnchorForTarget(t, altLeftOffset, altRightOffset, altTopOffset, altBottomOffset);
            }
        }
        else
        {
            base.MatchAnchorsToOuterGlobal();
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
        UpdateBucketsActive();
        MatchAnchorsToOuterGlobal();
    }
#endif

    private void UpdateBucketsActive()
    {
#if UNITY_EDITOR
        if (defaultObjects != null)
        {
            foreach (var go in defaultObjects)
                if (go) go.SetActive(!useAltStretchTarget);
        }

        if (altObjects != null)
        {
            foreach (var go in altObjects)
                if (go) go.SetActive(useAltStretchTarget);
        }
#endif
    }
}