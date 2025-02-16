using UnityEngine;

[ExecuteInEditMode]
public class UserTimer : MonoBehaviour
{
    [Header("Timer")]
    [SerializeField] private TimeType timerType;
    [SerializeField] private float updateFrequency = 50f;

    [Header("Color")]
    [SerializeField] private Color integerTextColor = Color.white;
    [SerializeField] private Color decimalTextColor = Color.white;

    [Header("References")]
    [SerializeField] private TMPro.TMP_Text integerText;
    [SerializeField] private TMPro.TMP_Text decimalText;
    [SerializeField] private TMPro.TMP_Text unitText;

    private Timer timer;
    private float currentValue;

    public void ResetTimer()
    {
        currentValue = 0;
        timer.Reset();
        UpdateDisplay();
    }

    private void Start()
    {
        timer = new Timer(0, timerType, true, 0);
        UpdateStaticDisplay();
        StartCoroutine(UpdateTimerCoroutine());
    }

    private System.Collections.IEnumerator UpdateTimerCoroutine()
    {
        while(true)
        {
            yield return new WaitForSeconds(1f / updateFrequency);
            currentValue = timer.GetTime();
            UpdateDisplay();
        }
    }

    private void UpdateDisplay()
    {
        int integerPart = (int)currentValue;
        int decimalPart = (int)((currentValue - integerPart) * 100);
        integerText.text = integerPart.ToString();
        decimalText.text = decimalPart.ToString("D2");
    }

    private void UpdateStaticDisplay()
    {
        integerText.color = integerTextColor;
        decimalText.color = decimalTextColor;
        unitText.text = "s";
    }

    private void Update()
    {
        if(!Application.isPlaying)
        {
            currentValue = 0;
            UpdateStaticDisplay();
            UpdateDisplay();
        }
    }

    private void OnOestroy() => StopCoroutine(UpdateTimerCoroutine());
}