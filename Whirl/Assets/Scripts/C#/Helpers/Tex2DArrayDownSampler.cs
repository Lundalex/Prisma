// Assets/Tex2DArrayDownsamplerSimple.cs
// Minimal: pick a source Texture2DArray, set target dims, flip "Generate Now" to produce a smaller array.
// Saves an .asset and assigns it to 'output'.
// Note: generation & saving run in Editor only; no fancy UI.

using System;
using UnityEngine;

public class Tex2DArrayDownsamplerSimple : MonoBehaviour
{
    [Header("Source / Output")]
    public Texture2DArray source;
    public Texture2DArray output; // result is assigned here after generation

    [Header("Target Dimensions (clamped to source)")]
    public int targetWidth  = 0;  // 0 = half of source (rounded up)
    public int targetHeight = 0;  // 0 = half of source (rounded up)
    public int targetLayers = 0;  // 0 = half of source (rounded up)

    [Header("Options")]
    public bool generateMipmaps = false;  // mips on the output
    public FilterMode outputFilter = FilterMode.Bilinear;

    [Header("Save Location")]
    public string saveFolder = "Assets";
    public string outputName = "Tex2DArray_downsampled.asset";
    public bool overwriteExisting = true;

    [Header("Action")]
    public bool generateNow = false; // toggle this to run

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!generateNow) return;
        generateNow = false;

        if (source == null)
        {
            Debug.LogError("Tex2DArrayDownsampler: No source Texture2DArray assigned.");
            return;
        }

        if (!source.isReadable)
        {
            Debug.LogWarning("Tex2DArrayDownsampler: Source array is not marked readable. " +
                             "GetPixels may fail. Consider rebuilding it uncompressed/readable.");
        }

        int sw = source.width;
        int sh = source.height;
        int sl = source.depth;

        int dw = Mathf.Clamp(targetWidth  > 0 ? targetWidth  : (sw + 1) / 2, 1, sw);
        int dh = Mathf.Clamp(targetHeight > 0 ? targetHeight : (sh + 1) / 2, 1, sh);
        int dl = Mathf.Clamp(targetLayers > 0 ? targetLayers : (sl + 1) / 2, 1, sl);

        try
        {
            var dst = Downsample(source, dw, dh, dl, generateMipmaps);
            dst.wrapMode = source.wrapMode;
            dst.filterMode = outputFilter;
            dst.anisoLevel = source.anisoLevel;

            // Save & assign
            var path = CombineAssetPath(saveFolder, outputName);
            EnsureFolder(saveFolder);

            if (overwriteExisting && UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2DArray>(path) != null)
                UnityEditor.AssetDatabase.DeleteAsset(path);

            UnityEditor.AssetDatabase.CreateAsset(dst, path);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.Refresh();

            output = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2DArray>(path);
            UnityEditor.EditorGUIUtility.PingObject(output);
            Debug.Log($"Tex2DArrayDownsampler: Saved {dw}x{dh}x{dl} to {path}");
        }
        catch (Exception e)
        {
            Debug.LogError($"Tex2DArrayDownsampler: Downsample failed.\n{e}");
        }
    }
#endif

    // ---------------- Core downsample (Editor/runtime safe) ----------------

    public static Texture2DArray Downsample(Texture2DArray src, int dstWidth, int dstHeight, int dstLayers, bool generateMipmaps)
    {
        if (src == null) throw new ArgumentNullException(nameof(src));
        if (dstWidth < 1 || dstHeight < 1 || dstLayers < 1) throw new ArgumentException("Invalid target dimensions.");
        if (dstLayers > src.depth) throw new ArgumentException("dstLayers cannot exceed source layers.");

        int sw = src.width, sh = src.height, sl = src.depth;

        var dst = new Texture2DArray(dstWidth, dstHeight, dstLayers, src.format, generateMipmaps, /*linear*/ false);

        var downXY = new Color[dstWidth * dstHeight];
        var accum  = new Vector4[dstWidth * dstHeight];
        var outC32 = new Color32[dstWidth * dstHeight];

        for (int dz = 0; dz < dstLayers; dz++)
        {
            float srcZ0f =  (dz      * sl) / (float)dstLayers;
            float srcZ1f = ((dz + 1) * sl) / (float)dstLayers;
            int   srcZ0  = Mathf.FloorToInt(srcZ0f);
            int   srcZ1  = Mathf.CeilToInt (srcZ1f);
            srcZ0 = Mathf.Clamp(srcZ0, 0, sl - 1);
            srcZ1 = Mathf.Clamp(srcZ1, 1, sl);

            Array.Clear(accum, 0, accum.Length);
            int count = Mathf.Max(1, srcZ1 - srcZ0);

            for (int sz = srcZ0; sz < srcZ1; sz++)
            {
                var srcPixels = src.GetPixels32(sz);
                BilinearDownscaleXY(srcPixels, sw, sh, downXY, dstWidth, dstHeight);

                for (int i = 0; i < downXY.Length; i++)
                {
                    var c = downXY[i];
                    accum[i].x += c.r;
                    accum[i].y += c.g;
                    accum[i].z += c.b;
                    accum[i].w += c.a;
                }
            }

            float inv = 1f / count;
            for (int i = 0; i < accum.Length; i++)
            {
                var v = accum[i] * inv;
                byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(v.x * 255f), 0, 255);
                byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(v.y * 255f), 0, 255);
                byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(v.z * 255f), 0, 255);
                byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(v.w * 255f), 0, 255);
                outC32[i] = new Color32(r, g, b, a);
            }

            dst.SetPixels32(outC32, dz);
        }

        dst.Apply(generateMipmaps, false);
        return dst;
    }

    private static void BilinearDownscaleXY(Color32[] src, int srcW, int srcH, Color[] dst, int dstW, int dstH)
    {
        float scaleX = (float)srcW / dstW;
        float scaleY = (float)srcH / dstH;

        for (int y = 0; y < dstH; y++)
        {
            float sy = (y + 0.5f) * scaleY - 0.5f;
            int y0 = Mathf.FloorToInt(sy);
            int y1 = Mathf.Min(y0 + 1, srcH - 1);
            y0 = Mathf.Clamp(y0, 0, srcH - 1);
            float ty = Mathf.Clamp01(sy - Mathf.Floor(sy));

            for (int x = 0; x < dstW; x++)
            {
                float sx = (x + 0.5f) * scaleX - 0.5f;
                int x0 = Mathf.FloorToInt(sx);
                int x1 = Mathf.Min(x0 + 1, srcW - 1);
                x0 = Mathf.Clamp(x0, 0, srcW - 1);
                float tx = Mathf.Clamp01(sx - Mathf.Floor(sx));

                var c00 = src[y0 * srcW + x0];
                var c10 = src[y0 * srcW + x1];
                var c01 = src[y1 * srcW + x0];
                var c11 = src[y1 * srcW + x1];

                float r0 = Mathf.Lerp(c00.r, c10.r, tx);
                float g0 = Mathf.Lerp(c00.g, c10.g, tx);
                float b0 = Mathf.Lerp(c00.b, c10.b, tx);
                float a0 = Mathf.Lerp(c00.a, c10.a, tx);

                float r1 = Mathf.Lerp(c01.r, c11.r, tx);
                float g1 = Mathf.Lerp(c01.g, c11.g, tx);
                float b1 = Mathf.Lerp(c01.b, c11.b, tx);
                float a1 = Mathf.Lerp(c01.a, c11.a, tx);

                dst[y * dstW + x] = new Color(r: Mathf.Lerp(r0, r1, ty) / 255f,
                                              g: Mathf.Lerp(g0, g1, ty) / 255f,
                                              b: Mathf.Lerp(b0, b1, ty) / 255f,
                                              a: Mathf.Lerp(a0, a1, ty) / 255f);
            }
        }
    }

#if UNITY_EDITOR
    // ---------------- Editor helpers (no fancy UI) ----------------
    private static string CombineAssetPath(string folder, string name)
    {
        if (string.IsNullOrEmpty(folder)) folder = "Assets";
        if (!folder.StartsWith("Assets")) folder = "Assets";
        if (string.IsNullOrEmpty(name)) name = "Tex2DArray_downsampled.asset";
        if (!name.EndsWith(".asset")) name += ".asset";
        if (folder.EndsWith("/")) return folder + name;
        return folder + "/" + name;
    }

    private static void EnsureFolder(string folder)
    {
        if (string.IsNullOrEmpty(folder)) return;
        if (UnityEditor.AssetDatabase.IsValidFolder(folder)) return;

        // Create nested folders under Assets
        string[] parts = folder.Split('/');
        if (parts.Length == 0 || parts[0] != "Assets") return;

        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = current + "/" + parts[i];
            if (!UnityEditor.AssetDatabase.IsValidFolder(next))
                UnityEditor.AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }
#endif
}