using UnityEngine;
using UnityEngine.UI;

public class UserURL : MonoBehaviour
{
    [Header("URL")]
    [SerializeField] private string url = "URL Here";

    [Header("Refrences")]
    [SerializeField] private Image backgroundImage;

    void Awake()
    {
        MakeTransparent(ref backgroundImage);
    }

    private void MakeTransparent(ref Image image)
    {
        Color color = image.color;
        color.a = 0f;
        image.color = color;
    }

    public void OpenURL() => LinkHandler.OpenURL(url);
}