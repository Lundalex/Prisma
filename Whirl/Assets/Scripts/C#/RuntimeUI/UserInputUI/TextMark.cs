using TMPro;
using UnityEngine;

[ExecuteInEditMode]
public class TextMark : MonoBehaviour
{
    [SerializeField] private RectTransform mark;
    [SerializeField] private TMP_Text targetText;
    [SerializeField] private Vector2 offset; // optional nudge away from the corner

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
        {
            PositionMarkAtTextTopRight();
        }
    }
#endif

    void PositionMarkAtTextTopRight()
    {
        if (mark == null || targetText == null) return;

        // Ensure geometry is up-to-date (important in Edit Mode).
        targetText.ForceMeshUpdate(true, true);

        var textRT = targetText.rectTransform;
        var parentRT = mark.parent as RectTransform;
        if (parentRT == null) return;

        var info = targetText.textInfo;
        int count = info == null ? 0 : info.characterCount;

        bool hasVisible = false;
        float minX = float.PositiveInfinity;
        float minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity;
        float maxY = float.NegativeInfinity;

        // Gather bounds from visible characters
        for (int i = 0; i < count; i++)
        {
            var ch = info.characterInfo[i];
            if (!ch.isVisible) continue;
            hasVisible = true;

            // Update full bounds from character quad
            if (ch.bottomLeft.x < minX) minX = ch.bottomLeft.x;
            if (ch.topLeft.x    < minX) minX = Mathf.Min(minX, ch.topLeft.x);

            if (ch.bottomRight.x > maxX) maxX = ch.bottomRight.x;
            if (ch.topRight.x    > maxX) maxX = Mathf.Max(maxX, ch.topRight.x);

            if (ch.bottomLeft.y < minY) minY = ch.bottomLeft.y;
            if (ch.bottomRight.y < minY) minY = Mathf.Min(minY, ch.bottomRight.y);

            if (ch.topLeft.y > maxY) maxY = ch.topLeft.y;
            if (ch.topRight.y > maxY) maxY = Mathf.Max(maxY, ch.topRight.y);
        }

        Vector2 topRightLocal;

        if (hasVisible)
        {
            // Top-right of the rendered glyph bounds in the text's local space
            topRightLocal = new Vector2(maxX, maxY);
        }
        else
        {
            // Fallback if there is no visible text: use the RectTransform's top-right
            var rect = textRT.rect;
            var p = textRT.pivot;
            float x = (1f - p.x) * rect.width;
            float y = (1f - p.y) * rect.height;
            topRightLocal = new Vector2(x, y);
        }

        // Convert text local -> world -> parent local
        Vector3 world = textRT.TransformPoint(topRightLocal);
        Vector2 parentLocal = parentRT.InverseTransformPoint(world);

        // Place the mark so its pivot sits at the text's top-right corner (plus optional offset)
        mark.anchoredPosition = parentLocal + offset;
    }
}