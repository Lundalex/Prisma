using UnityEngine;

[CreateAssetMenu(menuName = "CustomMats/RenderMat", fileName = "RenderMat")]
public class RenderMat : CustomMat
{
    [Header("Generation Settings")]
    public Material material;

    [Range(0, 10)] public float light;

    public Texture2D bakedTexture;
}