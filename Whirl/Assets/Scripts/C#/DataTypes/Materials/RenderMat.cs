using UnityEngine;

[CreateAssetMenu(menuName = "CustomMats/RenderMat", fileName = "RenderMat")]
public class RenderMat : CustomMat
{
#if UNITY_EDITOR
    [Header("Generation Settings (Editor-only)")]
    public Material material;
#endif
    [Range(0, 10)] public float light;

    [Tooltip("Result of the bake; used at runtime to build the atlas.")]
    public Texture2D bakedTexture;
}