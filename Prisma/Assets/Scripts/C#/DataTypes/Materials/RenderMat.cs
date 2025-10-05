using UnityEngine;
#if UNITY_EDITOR
using Unity.Mathematics;
#endif

[CreateAssetMenu(menuName = "CustomMats/RenderMat", fileName = "RenderMat")]
public class RenderMat : CustomMat
{
#if UNITY_EDITOR
    [Header("Generation Settings (Editor-only)")]
    public Material material;
    public int2 bakeResolution = new(1080, 1080);
#endif
    [Range(0, 10)] public float light;

    [Tooltip("Result of the bake; used at runtime to build the atlas.")]
    public Texture2D bakedTexture;
}