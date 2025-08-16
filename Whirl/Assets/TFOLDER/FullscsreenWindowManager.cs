using UnityEngine;

[ExecuteInEditMode]
public class FullscsreenWindowManager : MonoBehaviour
{
    [Header("Sizing")]
    [SerializeField, Min(0)] float navBarButtonScale;
    [SerializeField, Min(0)] float navBarHeight;
    [SerializeField, Min(0)] float workAreaWidth;
    [SerializeField, Min(0)] float separatorPadding;

    [Header("References")]
    [SerializeField] RectTransform windowContainer;
    [SerializeField] RectTransform[] windows;
    [SerializeField] RectTransform buttonContainer;
    [SerializeField] RectTransform[] buttons;
    [SerializeField] RectTransform headerOutline;
    [SerializeField] RectTransform[] separatorTrims;

    void Start()
    {
        UpdateContents();
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
        {
            UpdateContents();
        }
    }
#endif

    void UpdateContents()
    {
        // 1) Set the navbar height
        if (buttonContainer != null)
        {
            buttonContainer.localScale = Vector3.one * navBarButtonScale;
            buttonContainer.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, navBarHeight / navBarButtonScale);
        }

        // 2) Set the top offset of the windowContainer to navBarHeight
        if (windowContainer != null)
        {
            var offMax = windowContainer.offsetMax; // top/right
            offMax.y = -navBarHeight;               // top inset is negative
            windowContainer.offsetMax = offMax;
        }

        // 3) Set each window's width to workAreaWidth and center it on X
        if (windows != null)
        {
            for (int i = 0; i < windows.Length; i++)
            {
                var w = windows[i];
                if (w == null) continue;

                // Center on X axis
                var min = w.anchorMin;
                var max = w.anchorMax;
                min.x = 0.5f;
                max.x = 0.5f;
                w.anchorMin = min;
                w.anchorMax = max;

                var pivot = w.pivot;
                pivot.x = 0.5f;
                w.pivot = pivot;

                // Apply width
                w.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, workAreaWidth);

                // Ensure centered horizontally
                var pos = w.anchoredPosition;
                pos.x = 0f;
                w.anchoredPosition = pos;
            }
        }

        // 4) Set the rect for the header
        if (headerOutline != null)
        {
            // Make sure horizontal anchors are stretch so left/right offsets apply
            var aMin = headerOutline.anchorMin;
            var aMax = headerOutline.anchorMax;
            aMin.x = 0f;
            aMax.x = 1f;
            headerOutline.anchorMin = aMin;
            headerOutline.anchorMax = aMax;

            // Compute side inset so width equals workAreaWidth when parent is 1920 wide:
            // k = (1920 - workAreaWidth) / 2 = 960 - 0.5 * workAreaWidth
            float sideInset = 960f - 0.5f * workAreaWidth;

            var offMin = headerOutline.offsetMin; // left/bottom
            var offMax = headerOutline.offsetMax; // right/top

            // Bottom offset: 1083 - navBarHeight
            offMin.y = 1077f - navBarHeight;

            // Left/right offsets
            offMin.x = sideInset;      // left inset
            offMax.x = -sideInset;     // right inset is negative

            headerOutline.offsetMin = offMin;
            headerOutline.offsetMax = offMax;
        }

        // 5) Set each separator trim's width to (workAreaWidth - separatorPadding)
        if (separatorTrims != null)
        {
            float targetWidth = Mathf.Max(0f, workAreaWidth - separatorPadding);
            for (int i = 0; i < separatorTrims.Length; i++)
            {
                var t = separatorTrims[i];
                if (t == null) continue;

                // Apply width
                t.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

                var pos = t.anchoredPosition;
                pos.x = 0f;
                t.anchoredPosition = pos;
            }
        }
    }
}