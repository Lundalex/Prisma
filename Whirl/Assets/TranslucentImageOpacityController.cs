using UnityEngine;
using LeTai.Asset.TranslucentImage;

[DisallowMultipleComponent]
public class TranslucentImageOpacityController : MonoBehaviour
{
    [Range(0f, 1f)]
    [SerializeField] private float opacity = 0.5f;

    private float _lastApplied = -1f;

    private void OnEnable()
    {
        if (Application.isPlaying)
        {
            Apply();
        }
    }

    private void Update()
    {
        if (!Application.isPlaying) return;

        if (!Mathf.Approximately(_lastApplied, opacity))
        {
            Apply();
        }
    }

    private void Apply()
    {
        var images = Object.FindObjectsByType<TranslucentImage>(FindObjectsSortMode.None);
        for (int i = 0; i < images.Length; i++)
        {
            images[i].foregroundOpacity = opacity;
        }
        _lastApplied = opacity;
    }
}