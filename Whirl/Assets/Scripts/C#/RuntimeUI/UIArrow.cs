using Resources2;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[ExecuteInEditMode]
public class UIArrow : EditorLifeCycle
{
    [Header("Arrow Position")]
    [SerializeField] private Vector2 center = Vector2.zero;
    [SerializeField] private float radius = 20f;

    [Header("Value Display")]
    [SerializeField] private float displayBoxScale = 1f;
    [SerializeField] private float value = 0f;
    [SerializeField] private float minValueForArrow = 0f;
    [SerializeField, Range(1, 2)] private int numDecimals = 2;
    [SerializeField] private float rotation = 0f;
    [SerializeField] private string unit = "m/s";
    [SerializeField, Range(0f, 1f)] private float colorLerpFactor = 0;

    [Header("Arrow Dimensions")]
    [SerializeField] private float baseWidth = 20f;
    [SerializeField] private float baseLength = 100f;
    [SerializeField] private float hatSize = 80f;
    [SerializeField] private float outlineGap = 3f;

    [Header("Colors")]
    [SerializeField] private Color minOutlineColor;
    [SerializeField] private Color maxOutlineColor;
    [SerializeField] private Color minBodyColor;
    [SerializeField] private Color maxBodyColor;
    [SerializeField] private Color displayBoxColor;

    [Header("References")]
    // Containers
    [SerializeField] private RectTransform rotationJointRect;
    [SerializeField] private RectTransform positionJointRect;
    [SerializeField] private RectTransform spriteContainerRect;
    [SerializeField] private RectTransform displayBoxRect;
    // Outline
    [SerializeField] private RectTransform outlineBaseRect;
    [SerializeField] private RectTransform outlineHatRect;
    // Body
    [SerializeField] private RectTransform bodyBaseRect;
    [SerializeField] private RectTransform bodyHatRect;
    // Images
    [SerializeField] private Image outlineBaseImage;
    [SerializeField] private Image outlineHatImage;
    [SerializeField] private Image bodyBaseImage;
    [SerializeField] private Image bodyHatImage;
    // Display texts
    [SerializeField] private TMP_Text integerText;
    [SerializeField] private TMP_Text decimalText;
    [SerializeField] private TMP_Text unitText;

#if UNITY_EDITOR
    public override void OnEditorUpdate()
    {
        if (!Application.isPlaying) UpdateSprites();
    }
#endif

    public void SetConfig(
        Vector2 center,
        float radius,
        float displayBoxScale,
        float value,
        int numDecimals,
        float rotation,
        string unit,
        float baseWidth,
        float baseLength,
        float hatSize,
        float outlineGap,
        Color minOutlineColor,
        Color maxOutlineColor,
        Color minBodyColor,
        Color maxBodyColor,
        Color displayBoxColor)
    {
        this.center          = center;
        this.radius          = radius;
        this.displayBoxScale = displayBoxScale;
        this.value           = value;
        this.numDecimals     = numDecimals;
        this.rotation        = rotation;
        this.unit            = unit;
        this.baseWidth       = baseWidth;
        this.baseLength      = baseLength;
        this.hatSize         = hatSize;
        this.outlineGap      = outlineGap;
        this.minOutlineColor = minOutlineColor;
        this.maxOutlineColor = maxOutlineColor;
        this.minBodyColor    = minBodyColor;
        this.maxBodyColor    = maxBodyColor;
        this.displayBoxColor = displayBoxColor;

        UpdateSprites();
    }

    public void UpdateArrow(float val, string unit, float baseLength, float colorLerpFactor)
    {
        SetDisplayValue(val, unit);
        this.baseLength = baseLength;
        this.colorLerpFactor = colorLerpFactor;
        UpdateSprites();
    }

    public void SetPosition(Vector2 center, float rotation)
    {
        this.rotation = rotation;
        this.center = center;
        UpdateSprites();
    }

    public void SetValueBoxVisibility(bool setVisible)
    {
        displayBoxRect.gameObject.SetActive(setVisible);
    }

    public (Vector2 center, float rotation) GetPosition() => (this.center, this.rotation);

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
        outlineBaseImage.color = outlineHatImage.color = Color.Lerp(minOutlineColor, maxOutlineColor, colorLerpFactor);
        bodyBaseImage.color = bodyHatImage.color = Color.Lerp(minBodyColor, maxBodyColor, colorLerpFactor);

        // Unit texts
        integerText.color = decimalText.color = displayBoxColor;

        // Display box
        displayBoxRect.localScale = new(displayBoxScale, displayBoxScale);

        SetDisplayValue(value, unit);
    }

    private void SetDisplayValue(float newValue, string newUnit)
    {
        this.value = newValue;
        this.unit = newUnit;
        if (value < 0.0f)
        {
            value = Mathf.Abs(value);
            unit = "-" + unit;
        }

        int integerPart = Mathf.Clamp((int)value, -999, 999); // Clamp to max 3 characters
        int decimalPart = Mathf.Clamp(Mathf.RoundToInt(Mathf.Abs(value - integerPart) * Mathf.Pow(10, numDecimals)), 0, (int)Mathf.Pow(10, numDecimals) - 1);

        // Update the UI text fields
        integerText.text = integerPart.ToString();
        decimalText.text = decimalPart.ToString($"D{numDecimals}");
        unitText.text = unit;

        if (Application.isPlaying)
        {
            bool isVisible = value >= minValueForArrow;
            rotationJointRect.gameObject.SetActive(isVisible);
        }
    }
}
