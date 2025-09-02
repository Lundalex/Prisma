using UnityEngine; 

[CreateAssetMenu(menuName = "Rendering/RenderMat", fileName = "RenderMat")]
public class RenderMat : ScriptableObject
{
    public Texture2D bakedTexture;
    public Material material;
}