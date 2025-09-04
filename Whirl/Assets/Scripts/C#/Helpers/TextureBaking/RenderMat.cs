using UnityEngine;

[CreateAssetMenu(menuName = "Rendering/RenderMat", fileName = "RenderMat")]
public class RenderMat : BaseMat
{
    [Header("Generation Settings")]
    public Material material;
    [Range(0, 5)] public float light;

    public Texture2D bakedTexture;
}