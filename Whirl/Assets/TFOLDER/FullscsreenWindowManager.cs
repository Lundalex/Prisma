using UnityEngine;

[ExecuteInEditMode]
public class FullscsreenWindowManager : MonoBehaviour
{
    [Header("Sizing")]
    [SerializeField, Min(0)] float navBarButtonScale;
    [SerializeField, Min(0)] float navBarHeight;
    [SerializeField, Min(0)] float workAreaWidth;

    [Header("References")]
    [SerializeField] RectTransform windowContainer;
    [SerializeField] RectTransform[] windows;
    [SerializeField] RectTransform buttonContainer;
    [SerializeField] RectTransform[] buttons;

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
    }
}