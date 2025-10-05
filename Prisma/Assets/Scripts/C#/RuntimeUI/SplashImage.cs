using Resources2;
using UnityEngine;
using UnityEngine.UI;
using PM = ProgramManager;

public class SplashImage : MonoBehaviour
{
    // Inspector
    [SerializeField] private bool doEditorAnimation;
    [SerializeField] private Image image;

    private static readonly float fadeDuration = 1.0f;

    // Nonserialized
    private float timePassed = 0.0f;

    private void OnEnable()
    {
        if (Application.isEditor && !doEditorAnimation || PM.hasShownStartConfirmation) 
        {
            gameObject.SetActive(false);
            return;
        }
    }

    private void Update()
    {
        timePassed += Mathf.Min(Time.deltaTime, PM.MaxDeltaTime);

        float opcaity = 1 - Mathf.Min(timePassed / fadeDuration, 1);
        SetImageOpacity(opcaity);

        if (timePassed > fadeDuration) gameObject.SetActive(false);
    }

    private void SetImageOpacity(float opacity)
    {
        image.color = new Color(image.color.r, image.color.g, image.color.b, opacity);
    }
}
