using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class FullscsreenWindowManager : MonoBehaviour
{
    [Header("Sizing")]
    [SerializeField, Min(0)] float navBarButtonScale;
    [SerializeField, Min(0)] float navBarHeight;
    [SerializeField, Min(0)] float workAreaWidth;
    [SerializeField, Min(0)] float answerAreaPadding;
    [SerializeField, Min(0)] float separatorPadding;
    [SerializeField, Min(0)] float switchBetweenTasksSpacing;

    [Header("References")]
    [SerializeField] RectTransform windowContainer;
    [SerializeField] RectTransform[] windows;
    [SerializeField] HorizontalLayoutGroup[] switchBetweenTasksContainer;
    [SerializeField] RectTransform[] separatorTrims;
    [SerializeField] RectTransform[] answerAreas;

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

        // 6) Set each answer area's width to (workAreaWidth - answerAreaPadding)
        if (answerAreas != null && answerAreas.Length > 0)
        {
            float targetWidth = Mathf.Max(0f, workAreaWidth - answerAreaPadding);
            for (int i = 0; i < answerAreas.Length; i++)
            {
                var rt = answerAreas[i];
                if (rt == null) continue;

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, targetWidth);

                var pos = rt.anchoredPosition;
                pos.x = 0f;
                rt.anchoredPosition = pos;
            }
        }

        // 7) Set spacing for ALL "switch between tasks" HorizontalLayoutGroups
        if (switchBetweenTasksContainer != null && switchBetweenTasksContainer.Length > 0)
        {
            for (int i = 0; i < switchBetweenTasksContainer.Length; i++)
            {
                var hlg = switchBetweenTasksContainer[i];
                if (hlg == null) continue;

                hlg.spacing = switchBetweenTasksSpacing;

                #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        if (hlg.TryGetComponent<RectTransform>(out var rt))
                            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
                    }
                #endif
            }
        }
    }
}