#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class Texture2DArrayCombiner
{
    public static void CreateResizedTexture2DArray(
        string folderPath,
        string destPath,
        string suffix,
        TextureFormat textureFormat,
        int targetWidth,
        int targetHeight,
        int targetDepth
    )
    {
        string[] jpgPaths = Directory.GetFiles(folderPath, "*.jpg");
        if (jpgPaths.Length == 0) return;

        int totalImages = jpgPaths.Length;
        int depth = Mathf.Min(targetDepth, totalImages);
        float step = (float)totalImages / depth;

        Texture2D[] textures = new Texture2D[depth];
        for (int i = 0; i < depth; i++)
        {
            int sourceIndex = Mathf.FloorToInt(i * step);
            byte[] fileData = File.ReadAllBytes(jpgPaths[sourceIndex]);
            Texture2D originalTex = new(2, 2, textureFormat, false);
            originalTex.LoadImage(fileData);

            textures[i] = ScaleTexture(originalTex, targetWidth, targetHeight, textureFormat);
        }

        Texture2DArray textureArray = new(
            targetWidth, targetHeight, depth, textureFormat, false
        );
        for (int i = 0; i < depth; i++)
            Graphics.CopyTexture(textures[i], 0, 0, textureArray, i, 0);

        AssetDatabase.CreateAsset(textureArray, destPath + "/Tex2DArray_" + suffix + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Texture2D ScaleTexture(Texture2D source, int tw, int th, TextureFormat format)
    {
        RenderTexture rt = RenderTexture.GetTemporary(tw, th);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new(tw, th, format, false);
        result.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}
#endif