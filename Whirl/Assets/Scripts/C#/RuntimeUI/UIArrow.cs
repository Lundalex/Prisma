using Resources2; 
using TMPro;
using UnityEngine;
using Image = UnityEngine.UI.Image;
using PM = ProgramManager;

[ExecuteInEditMode]
public class UIArrow : EditorLifeCycle
{
    [Header("Arrow Transform")]
    public Vector2 center = Vector2.zero;
    public Vector2 centerOffset = Vector2.zero;
    public float radius = 20f;
    public float rotation = 0f;
    public float scale = 1f;

    [Header("Value Display")]
    [SerializeField] private float displayBoxScale = 1f;
    [SerializeField] private float value = 0f;
    [SerializeField] private float valueFactor10Exp = 0;
    [SerializeField, Range(1, 3)] private int numIntegers = 3;
    [SerializeField] private DisplayPrecision displayPrecision = DisplayPrecision.Int_Precise;
    [SerializeField] private bool overrideUnit;
    [SerializeField] private string unit = "m/s";
    [SerializeField, Range(0f, 1f)] private float colorLerpFactor = 0;

    [Header("Radius Oscillation")]
    [SerializeField] private float radiusOscillationRadius;
    [SerializeField] private float radiusOscillationSpeed;

    [Header("Arrow Dimensions")]
    [SerializeField] private float baseWidth = 20f;
    [SerializeField] private float baseLength = 100f;
    [SerializeField] private float hatSize = 80f;
    [SerializeField] private float outlineGap = 3f;
    [SerializeField] private bool extrudeForward = true;

    [Header("Colors")]
    [SerializeField] private bool doUseHSVColorLerp;
    [SerializeField] private Color minOutlineColor;
    [SerializeField] private Color maxOutlineColor;
    [SerializeField] private Color minBodyColor;
    [SerializeField] private Color maxBodyColor;
    [SerializeField] private Color displayBoxColor;

    [Header("Update Thresholds")]
    [SerializeField] private float minValueDeltaForUpdate = 1f;
    [SerializeField] private float minRadiusDeltaForUpdate = 1f;
    [SerializeField] private float minRotationDeltaForUpdate = 1f;
    [SerializeField] private float minBaseLengthDeltaForUpdate = 1f;
    [SerializeField] private float minColorLerpFactorDeltaForUpdate = 0.01f;

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

    private float lastOscillation = 0;
    private bool displyBoxActive;
    private bool arrowActive;

#if UNITY_EDITOR
    public override void OnEditorUpdate()
    {
        if (!Application.isPlaying) UpdateSprites();
    }
#endif

    private void OnEnable()
    {
        displyBoxActive = displayBoxRect.gameObject.activeSelf;
        arrowActive = gameObject.activeSelf;
    }

    public void SetConfig(
        Vector2 center,
        float radius,
        float displayBoxScale,
        float value,
        int _ignoredNumDecimals, // kept for API compatibility; precision is driven by enum now
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
        this.center = center;
        this.radius = radius;
        this.displayBoxScale = displayBoxScale;
        this.value = value;
        this.rotation = rotation;
        this.unit = unit;
        this.baseWidth = baseWidth;
        this.baseLength = baseLength;
        this.hatSize = hatSize;
        this.outlineGap = outlineGap;
        this.minOutlineColor = minOutlineColor;
        this.maxOutlineColor = maxOutlineColor;
        this.minBodyColor = minBodyColor;
        this.maxBodyColor = maxBodyColor;
        this.displayBoxColor = displayBoxColor;

        UpdateSprites();
    }

    public void UpdateArrow(float val, string unit, float baseLength, float colorLerpFactor)
    {
        val *= Mathf.Pow(10, valueFactor10Exp);

        // Only update display value if the change is significant
        if (Mathf.Abs(val - this.value) > minValueDeltaForUpdate)
        {
            SetDisplayValue(val, unit);
            if (displyBoxActive) UpdateDisplayValue();
        }
        // Only update geometry (baseLength) if the change is significant
        if (Mathf.Abs(baseLength - this.baseLength) > minBaseLengthDeltaForUpdate)
        {
            this.baseLength = baseLength;
            UpdateGeometrySprites();
        }
        // Always update colorLerpFactor (assumed to be cheaper)
        if (Mathf.Abs(colorLerpFactor - this.colorLerpFactor) > minColorLerpFactorDeltaForUpdate)
        {
            this.colorLerpFactor = colorLerpFactor;
            UpdateColorAndTextSprites();
        }
    }

    public void SetCenterAndRotation(Vector2 center, float rotation)
    {
        SetCenter(center);
        SetRotation(rotation);
        this.center = center;
    }
    public void SetCenter(Vector2 center)
    {
        this.center = center;
        UpdateCenterSprites();
    }
    public void SetRadius(float newRadius)
    {
        float newOscillation = Func.SinOscillation(PM.Instance.totalScaledTimeElapsed * radiusOscillationSpeed) * radiusOscillationRadius;
        if (Mathf.Abs((newRadius + newOscillation) - (this.radius + lastOscillation)) > minRadiusDeltaForUpdate)
        {
            lastOscillation = newOscillation;
            this.radius = newRadius;
            UpdateRadiusSprites(newOscillation);
        }
    }
    public void SetRotation(float newRotation)
    {
        if (Mathf.Abs(newRotation - this.rotation) > minRotationDeltaForUpdate)
        {
            this.rotation = newRotation;
            UpdateRotationSprites();
        }
    }
    public void SetScale(float scale)
    {
        this.scale = scale;
        UpdateScaleSprites();
    }

    public void SetValueBoxVisibility(bool setVisible)
    {
        if (setVisible != displyBoxActive)
        {
            displayBoxRect.gameObject.SetActive(setVisible);
            displyBoxActive = setVisible;
        }
    }

    public void SetArrowVisibility(bool setVisible)
    {
        if (setVisible != arrowActive)
        {
            gameObject.SetActive(setVisible);
            arrowActive = setVisible;
        }
    }

    public (Vector2 center, float rotation) GetPosition() => (this.center, this.rotation);

    private void UpdateSprites()
    {
        UpdateTransformSprites();
        UpdateGeometrySprites();
        UpdateColorAndTextSprites();
        if (displyBoxActive) UpdateDisplayValue();
    }

    private void UpdateTransformSprites()
    {
        UpdateCenterSprites();
        UpdateRotationSprites();
        UpdateScaleSprites();
    }

    private void UpdateCenterSprites()
    {
        transform.localPosition = center + centerOffset;
    }

    private void UpdateRadiusSprites(float oscillation)
    {
        // Radius offset
        positionJointRect.localPosition = new Vector2(radius + oscillation, 0);
    }

    private void UpdateRotationSprites()
    {
        rotationJointRect.localRotation = Quaternion.Euler(0f, 0f, rotation);
    }

    private void UpdateScaleSprites()
    {
        positionJointRect.localScale = new Vector2(scale, scale);
    }

    private void UpdateGeometrySprites()
    {
        // Sprite container
        spriteContainerRect.localPosition = extrudeForward ? new Vector2(baseLength, 0) : new Vector2(-baseLength, 0);

        // Base section
        outlineBaseRect.sizeDelta = new Vector2(baseLength, baseWidth);
        bodyBaseRect.sizeDelta = new Vector2(baseLength, baseWidth - 2f * outlineGap);
        bodyBaseRect.localPosition = outlineBaseRect.localPosition + new Vector3(outlineGap, 0);

        // Radius offset
        float oscillation = Func.SinOscillation(PM.Instance.totalScaledTimeElapsed * radiusOscillationSpeed) * radiusOscillationRadius;
        positionJointRect.localPosition = new Vector2(radius + oscillation, 0);

        // Hat section
        float hatHeight = baseWidth + hatSize;
        outlineHatRect.sizeDelta = new Vector2(0.5f * hatHeight, hatHeight);
        bodyHatRect.sizeDelta = outlineHatRect.sizeDelta - new Vector2(outlineGap * (7 / 3f), outlineGap * (14 / 3f));
        bodyHatRect.localPosition = outlineHatRect.localPosition + new Vector3(outlineGap, 0);
    }

    private void UpdateColorAndTextSprites()
    {
        // Sprite colors
        if (doUseHSVColorLerp)
        {
            outlineBaseImage.color = outlineHatImage.color = Utils.HSVColorLerp(minOutlineColor, maxOutlineColor, colorLerpFactor);
            bodyBaseImage.color = bodyHatImage.color = Utils.HSVColorLerp(minBodyColor, maxBodyColor, colorLerpFactor);
        }
        else
        {
            outlineBaseImage.color = outlineHatImage.color = Color.Lerp(minOutlineColor, maxOutlineColor, colorLerpFactor);
            bodyBaseImage.color = bodyHatImage.color = Color.Lerp(minBodyColor, maxBodyColor, colorLerpFactor);
        }

        if (displyBoxActive)
        {
            // Unit texts
            integerText.color = decimalText.color = displayBoxColor;

            // Display box
            displayBoxRect.localScale = new Vector2(displayBoxScale, displayBoxScale);
        }
    }

    private void UpdateDisplayValue()
    {
        // Determine decimals from enum
        int decimals = GetNumDecimalsFromPrecision(displayPrecision);

        // Update the UI text fields
        int maxInteger = (int)Mathf.Pow(10, numIntegers) - 1; // Determine max value based on numIntegers
        int integerPart = Mathf.Clamp((int)value, -maxInteger, maxInteger);

        if (decimals > 0)
        {
            int pow = (int)Mathf.Pow(10, decimals);
            int decimalPart = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Abs(value - integerPart) * pow),
                0, pow - 1
            );

            integerText.text = integerPart.ToString();
            decimalText.text = decimalPart.ToString($"D{decimals}");

        }
        else
        {
            // No decimals visible
            integerText.text = integerPart.ToString();
            decimalText.text = "__";
        }

        unitText.text = unit;
    }

    private void SetDisplayValue(float newValue, string newUnit)
    {
        // Quantize/round the ABSOLUTE magnitude according to the selected precision.
        bool isNegative = newValue < 0f;
        float absVal = Mathf.Abs(newValue);

        float rounded = Quantize(absVal, displayPrecision);

        // Store positive magnitude; negativity encoded into unit if not overridden
        this.value = rounded;

        if (!overrideUnit)
        {
            this.unit = isNegative ? "-" + newUnit : newUnit;
        }
    }

    // ---------- Helpers ----------

    private static int GetNumDecimalsFromPrecision(DisplayPrecision p)
    {
        return p switch
        {
            DisplayPrecision.Int_Halfs or DisplayPrecision.Int_Precise => 0,
            DisplayPrecision.OneDec_Halfs or DisplayPrecision.OneDec_Precise => 1,
            DisplayPrecision.TwoDec_Halfs or DisplayPrecision.TwoDec_Precise => 2,
            _ => 0,
        };
    }

    private static float Quantize(float v, DisplayPrecision p)
    {
        return p switch
        {
            DisplayPrecision.Int_Precise => Mathf.Round(v),// step 1
            DisplayPrecision.Int_Halfs => Mathf.Round(v / 5f) * 5f,// step 5
            DisplayPrecision.OneDec_Precise => Mathf.Round(v * 10f) / 10f,// step 0.1
            DisplayPrecision.OneDec_Halfs => Mathf.Round(v * 2f) / 2f,// step 0.5
            DisplayPrecision.TwoDec_Precise => Mathf.Round(v * 100f) / 100f,// step 0.01
            DisplayPrecision.TwoDec_Halfs => Mathf.Round(v * 20f) / 20f,// step 0.05
            _ => v, // Full precision
        };
    }
}

public enum DisplayPrecision
{
    Int_Halfs,
    Int_Precise,
    OneDec_Halfs,
    OneDec_Precise,
    TwoDec_Halfs,
    TwoDec_Precise
}