#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

public static class Texture3DCombinerWithResize
{
    public static void CreateResizedTexture3D(
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
            Texture2D originalTex = new Texture2D(2, 2, textureFormat, false);
            originalTex.LoadImage(fileData);

            textures[i] = ScaleTexture(originalTex, targetWidth, targetHeight, textureFormat);
        }

        Texture3D texture3D = new(targetWidth, targetHeight, depth, textureFormat, false);
        for (int z = 0; z < depth; z++)
        {
            Color[] pixels = textures[z].GetPixels();
            for (int y = 0; y < targetHeight; y++)
            {
                for (int x = 0; x < targetWidth; x++)
                    texture3D.SetPixel(x, y, z, pixels[y * targetWidth + x]);
            }
        }
        texture3D.Apply();

        AssetDatabase.CreateAsset(texture3D, destPath + "/Tex3D_" + suffix + ".asset");
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Texture2D ScaleTexture(Texture2D source, int tw, int th, TextureFormat format)
    {
        RenderTexture rt = RenderTexture.GetTemporary(tw, th);
        Graphics.Blit(source, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D result = new Texture2D(tw, th, format, false);
        result.ReadPixels(new Rect(0, 0, tw, th), 0, 0);
        result.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        return result;
    }
}
#endif