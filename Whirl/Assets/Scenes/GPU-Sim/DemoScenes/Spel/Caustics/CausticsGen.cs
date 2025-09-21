using UnityEngine;

public class CausticsGen : MonoBehaviour
{
    public Vector3Int texDims;
    public TextureFormat textureFormat;
    public bool true2DArray_false3D;
    public string suffix;

    #if UNITY_EDITOR
    public void Start()
    {
        string folderPath = Application.dataPath + "/Scenes/GPU-Sim/DemoScener/Spel/Caustics/Sequence";
        string destPath = "Assets/Scenes/GPU-Sim/DemoScener/Spel/Caustics/ComposedTextures";

        if (true2DArray_false3D) Texture2DArrayCombiner.CreateResizedTexture2DArray(folderPath, destPath, suffix, textureFormat, texDims.x, texDims.y, texDims.z);
        else Texture3DCombinerWithResize.CreateResizedTexture3D(folderPath, destPath, suffix, textureFormat, texDims.x, texDims.y, texDims.z);
    }
    #endif
}
