using UnityEngine;
using UnityEngine.UI;

public class UserURL : MonoBehaviour
{
    [Header("URL")]
    [SerializeField] private string url = "URL Here";

    [Header("Refrences")]
    [SerializeField] private Image backgroundImage;

    private void OnValidate()
    {
        SetTransparency(ref backgroundImage, (Application.isEditor && !Application.isPlaying) ? 50.0f / 255.0f : 0.0f);
    }
    private void OnEnable()
    {
        SetTransparency(ref backgroundImage, (Application.isEditor && !Application.isPlaying) ? 50.0f / 255.0f : 0.0f);
    }

    private void SetTransparency(ref Image image, float opacity)
    {
        Color color = image.color;
        color.a = opacity;
        image.color = color;
    }

    public void OpenURL() => LinkHandler.OpenURL(url);
}