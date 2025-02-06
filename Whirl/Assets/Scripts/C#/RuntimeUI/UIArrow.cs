using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class UIArrow : EditorLifeCycle
{
    [Header("Arrow Position")]
    [SerializeField] private Vector2 center = Vector2.zero;
    [SerializeField] private float radius = 20f;
    [SerializeField] private float rotation = 0f;

    [Header("Arrow Dimensions")]
    [SerializeField] private float baseWidth = 20f;
    [SerializeField] private float baseLength = 100f;
    [SerializeField] private float hatSize = 80f;
    [SerializeField] private float outlineGap = 3f;

    [Header("Arrow Colors")]
    [SerializeField] private Color outlineColor = Color.black;
    [SerializeField] private Color bodyColor = Color.white;

    [Header("References")]
    [SerializeField] private RectTransform rotationJointRect;
    [SerializeField] private RectTransform positionJointRect;
    [SerializeField] private RectTransform spriteContainerRect;
    [SerializeField] private RectTransform outlineBaseRect;
    [SerializeField] private RectTransform outlineHatRect;
    [SerializeField] private RectTransform bodyBaseRect;
    [SerializeField] private RectTransform bodyHatRect;

    [SerializeField] private Image outlineBaseImage;
    [SerializeField] private Image outlineHatImage;
    [SerializeField] private Image bodyBaseImage;
    [SerializeField] private Image bodyHatImage;

#if UNITY_EDITOR
    public override void OnEditorUpdate() => UpdateSprites();
#endif

    public void SetConfig(
        Vector2 center,
        float radius,
        float rotation,
        float baseWidth,
        float baseLength,
        float hatSize,
        float outlineGap,
        Color outlineColor,
        Color bodyColor)
    {
        this.center      = center;
        this.radius      = radius;
        this.rotation    = rotation;
        this.baseWidth   = baseWidth;
        this.baseLength  = baseLength;
        this.hatSize     = hatSize;
        this.outlineGap  = outlineGap;
        this.outlineColor = outlineColor;
        this.bodyColor    = bodyColor;

        UpdateSprites();
    }

    private void UpdateSprites()
    {
        // Joints
        rotationJointRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
        positionJointRect.localPosition = new(radius, 0);
        transform.localPosition = center;

        // Sprite container
        spriteContainerRect.localPosition = new(baseLength, 0);

        // Base section
        outlineBaseRect.sizeDelta = new(baseLength, baseWidth);
        bodyBaseRect.sizeDelta = new(baseLength, baseWidth - 2f*outlineGap);
        bodyBaseRect.localPosition = outlineBaseRect.localPosition + new Vector3(outlineGap, 0);

        // Hat section
        float hatHeight = baseWidth + hatSize;
        outlineHatRect.sizeDelta = new(0.5f*hatHeight, hatHeight);
        bodyHatRect.sizeDelta = outlineHatRect.sizeDelta - new Vector2(outlineGap*(7/3f), outlineGap*(14/3f));
        bodyHatRect.localPosition = outlineHatRect.localPosition + new Vector3(outlineGap, 0);

        // Sprite colors
        outlineBaseImage.color = outlineHatImage.color = outlineColor;
        bodyBaseImage.color = bodyHatImage.color = bodyColor;
    }
}
