using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class DualMultiContainer : MultiContainer
{
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
        if (!useAltStretchTarget)
        {
            base.MatchAnchorsToOuterGlobal();
            return;
        }

        float prevL = leftOffset, prevR = rightOffset, prevT = topOffset, prevB = bottomOffset;
        leftOffset = altLeftOffset; rightOffset = altRightOffset; topOffset = altTopOffset; bottomOffset = altBottomOffset;
        base.MatchAnchorsToOuterGlobal();
        leftOffset = prevL; rightOffset = prevR; topOffset = prevT; bottomOffset = prevB;
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
            {
                if (!go) continue;
                bool desired = !useAltStretchTarget;
                if (go.activeSelf != desired) go.SetActive(desired);
            }
        }

        if (altObjects != null)
        {
            foreach (var go in altObjects)
            {
                if (!go) continue;
                bool desired = useAltStretchTarget;
                if (go.activeSelf != desired) go.SetActive(desired);
            }
        }
#endif
    }
}