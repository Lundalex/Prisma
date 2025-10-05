// TexturePacker.cs
using UnityEngine;
using Unity.Mathematics;

public class TexturePacker : MonoBehaviour
{
    [Header("Compute")]
    [SerializeField] private ComputeShader packShader;
    [SerializeField] private int threadSizeXY = 8; // must match [numthreads(x,y,1)] in the compute

    public Texture2D PackTexturesIntoChannels(Texture2D r, Texture2D g, Texture2D b, Texture2D a)
    {
        // --- Validate inputs ---
        if (packShader == null)
        {
            Debug.LogError("TexturePacker: 'packShader' is not assigned.");
            return null;
        }
        if (r == null || g == null || b == null || a == null)
        {
            Debug.LogError("TexturePacker: All four input textures (r, g, b, a) must be non-null.");
            return null;
        }
        if (!SameSize(r, g) || !SameSize(r, b) || !SameSize(r, a))
        {
            Debug.LogError("TexturePacker: Input textures must all be the same size (width & height).");
            return null;
        }

        // --- Resolution & intermediate RT (via your helper) ---
        int2 resolution = new(r.width, r.height);
        RenderTexture rt = TextureHelper.CreateTexture(resolution, 3); // channels=3 -> R8G8B8A8_UNorm in your helper
        rt.name = "PackedRGBA_RT";

        // --- Bind & dispatch compute ---
        int kernel = packShader.FindKernel("PackRGBA_2D");
        packShader.SetInts("Resolution", resolution.x, resolution.y);
        packShader.SetTexture(kernel, "TexR", r);
        packShader.SetTexture(kernel, "TexG", g);
        packShader.SetTexture(kernel, "TexB", b);
        packShader.SetTexture(kernel, "TexA", a);
        packShader.SetTexture(kernel, "Texture_Output", rt);

        ComputeHelper.DispatchKernel(packShader, "PackRGBA_2D", resolution, threadSizeXY);

        var tex2D = new Texture2D(resolution.x, resolution.y, TextureFormat.RGBA32, false, true)
        {
            wrapMode = r.wrapMode,
            filterMode = r.filterMode
        };

        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        tex2D.ReadPixels(new Rect(0, 0, resolution.x, resolution.y), 0, 0, false);
        tex2D.Apply(false, false);
        RenderTexture.active = prev;

        // Free the GPU surface of the temporary RT
        ComputeHelper.Release(rt);

        return tex2D;
    }

    private static bool SameSize(Texture2D a, Texture2D b)
        => a.width == b.width && a.height == b.height;
}