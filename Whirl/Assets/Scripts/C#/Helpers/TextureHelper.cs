using Unity.Mathematics;
using UnityEngine;
using System;
using UnityEngine.Experimental.Rendering;

public class TextureHelper : MonoBehaviour
{
    public ComputeShader ngShader;
    public ComputeShader tcShader;
    private int ngShaderThreadSize = 8; // /~10
    private int tbShaderThreadSize = 8; // /~10
    [NonSerialized] public RenderTexture T_VectorMap;
    [NonSerialized] public RenderTexture T_TrilinearsMap;
    private int3 LastResolution;
    private int LastCellSize;

// --- CREATE TEXTURES (2D + 3D) ---

    public static RenderTexture CreateTexture(int3 resolution, int channels)
    {
        if (channels == 1)
        {
            RenderTexture texture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.R16) // 0.0-1.0 with linear accuracy, single channel
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();

            return texture;
        }

        else if (channels == 2)
        {
            RenderTexture texture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGFloat)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();

            return texture;
        }

        else // channels == 3
        {
            RenderTexture texture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGB111110Float)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();

            return texture;
        }
    }
    public static void CreateTexture(ref RenderTexture texture, int3 resolution, int channels)
    {
        if (channels == 1)
        {
            texture = texture != null ? texture : new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.R16) // 0.0-1.0 with linear accuracy, single channel
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
        }

        else if (channels == 2)
        {
            texture = texture != null ? texture : new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGFloat)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
        }

        else // channels == 3
        {
            texture = texture != null ? texture : new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGB111110Float)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex3D,
                volumeDepth = resolution.z,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
        }
    }
    public static RenderTexture CreateTexture(int2 resolution, int channels)
    {
        if (channels == 1)
        {
            RenderTexture texture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.R16)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
            return texture;
        }

        else if (channels == 2)
        {
            RenderTexture texture = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGFloat)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
            return texture;
        }
        else // channels == 3
        {
            RenderTexture texture = new RenderTexture(resolution.x, resolution.y, 0, GraphicsFormat.R8G8B8A8_UNorm)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };

            texture.Create();
            return texture;
        }
    }
    public static void CreateTexture(ref RenderTexture texture, int2 resolution, int channels)
    {
        if (channels == 1)
        {
            texture = texture != null ? texture : new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.R16)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
        }
        else if (channels == 2)
        {
            texture = texture != null ? texture : new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.RGFloat)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            };
            texture.Create();
        }
        else // channels == 3
        {
            texture = texture != null ? texture : new RenderTexture(resolution.x, resolution.y, 0, GraphicsFormat.R8G8B8A8_UNorm)
            {
                dimension = UnityEngine.Rendering.TextureDimension.Tex2D,
                enableRandomWrite = true,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point,
                useMipMap = false,
                autoGenerateMips = false,
                antiAliasing = 1
            };

            texture.Create();
        }
    }

	public static void TextureFromGradient(ref Texture2D texture, int width, Gradient gradient, FilterMode filterMode = FilterMode.Bilinear)
	{
		if (texture == null)
		{
			texture = new Texture2D(width, 1);
		}
		else if (texture.width != width)
		{
			texture.Reinitialize(width, 1);
		}
		if (gradient == null)
		{
			gradient = new Gradient();
			gradient.SetKeys(
				new GradientColorKey[] { new GradientColorKey(Color.black, 0), new GradientColorKey(Color.black, 1) },
				new GradientAlphaKey[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) }
			);
		}
		texture.wrapMode = TextureWrapMode.Clamp;
		texture.filterMode = filterMode;

		Color[] cols = new Color[width];
		for (int i = 0; i < cols.Length; i++)
		{
			float t = i / (cols.Length - 1f);
			cols[i] = gradient.Evaluate(t);
		}
		texture.SetPixels(cols);
		texture.Apply();
	}

// --- MODIFY TEXTURES (3D) ---

    public void Copy (ref RenderTexture texture, RenderTexture textureA, int3 resolution)
    {
        tcShader.SetTexture(0, "Texture_A", textureA);
        tcShader.SetTexture(0, "Texture_Output", texture);

        ComputeHelper.DispatchKernel (tcShader, "Copy_3D_F1", resolution, tbShaderThreadSize);
    }

    public void Blend (ref RenderTexture textureOutput, RenderTexture textureA, int3 resolution, float lerpWeight = 0.5f)
    {
        RenderTexture texCopy = CreateTexture(resolution, 1);
        Copy(ref texCopy, textureOutput, resolution);

        tcShader.SetFloat("LerpWeight", lerpWeight);

        tcShader.SetTexture(1, "Texture_A", textureA);
        tcShader.SetTexture(1, "Texture_B", texCopy);
        tcShader.SetTexture(1, "Texture_Output", textureOutput);

        ComputeHelper.DispatchKernel(tcShader, "Blend_3D_F1", resolution, tbShaderThreadSize);
    }

    public void Invert (ref RenderTexture texture, int3 resolution)
    {
        tcShader.SetTexture(2, "Texture_Output", texture);

        ComputeHelper.DispatchKernel(tcShader, "Invert_3D_F1", resolution, tbShaderThreadSize);
    }

    public void Saturate (ref RenderTexture texture, int3 resolution)
    {
        tcShader.SetTexture(3, "Texture_Output", texture);

        ComputeHelper.DispatchKernel(tcShader, "Saturate_3D_F1", resolution, tbShaderThreadSize);
    }

    public void ChangeBrightness (ref RenderTexture texture, int3 resolution, float brightnessFactor)
    {
        tcShader.SetTexture(4, "Texture_Output", texture);

        tcShader.SetFloat("BrightnessFactor", brightnessFactor);

        ComputeHelper.DispatchKernel(tcShader, "ChangeBrightness_3D_F1", resolution, tbShaderThreadSize);
    }

    public void AddBrightnessFixed (ref RenderTexture texture, int3 resolution, float brightnessAddOn)
    {
        tcShader.SetTexture(5, "Texture_Output", texture);

        tcShader.SetFloat("BrightnessAddOn", brightnessAddOn);

        ComputeHelper.DispatchKernel(tcShader, "AddBrightnessFixed_3D_F1", resolution, tbShaderThreadSize);
    }

    public void AddBrightnessByTexture (ref RenderTexture texture, RenderTexture textureA, int3 resolution, float brightnessFactor = 1.0f)
    {
        tcShader.SetTexture(6, "Texture_A", textureA);
        tcShader.SetTexture(6, "Texture_Output", texture);

        tcShader.SetFloat("BrightnessFactor", brightnessFactor);

        ComputeHelper.DispatchKernel(tcShader, "AddBrightnessByTexture_3D_F1", resolution, tbShaderThreadSize);
    }

    public void SubtractBrightnessByTexture (ref RenderTexture texture, RenderTexture textureA, int3 resolution, float brightnessFactor = 1.0f)
    {
        tcShader.SetTexture(7, "Texture_A", textureA);
        tcShader.SetTexture(7, "Texture_Output", texture);

        tcShader.SetFloat("BrightnessFactor", brightnessFactor);

        ComputeHelper.DispatchKernel(tcShader, "SubtractBrightnessByTexture_3D_F1", resolution, tbShaderThreadSize);
    }

    public void GaussianBlur (ref RenderTexture texture, int3 resolution, int smoothingRadius = 2, int iterations = 1)
    {
        for (int i = 0; i < iterations; i++)
        {
            RenderTexture texCopy = CreateTexture(resolution, 1);
            Copy(ref texCopy, texture, resolution);

            tcShader.SetTexture(8, "Texture_A", texCopy);
            tcShader.SetTexture(8, "Texture_Output", texture);

            tcShader.SetVector("Resolution", new Vector3(resolution.x, resolution.y, resolution.z));
            tcShader.SetFloat("SmoothingRadius", smoothingRadius);

            ComputeHelper.DispatchKernel(tcShader, "GaussianBlur_3D_F1", resolution, tbShaderThreadSize);
        }
    }

    public void BoxBlur (ref RenderTexture texture, int3 resolution, int smoothingRadius = 2, int iterations = 1)
    {
        for (int i = 0; i < iterations; i++)
        {
            RenderTexture texCopy = CreateTexture(resolution, 1);
            Copy(ref texCopy, texture, resolution);

            tcShader.SetTexture(9, "Texture_A", texCopy);
            tcShader.SetTexture(9, "Texture_Output", texture);

            tcShader.SetVector("Resolution", new Vector3(resolution.x, resolution.y, resolution.z));
            tcShader.SetFloat("SmoothingRadius", smoothingRadius);

            ComputeHelper.DispatchKernel(tcShader, "BoxBlur_3D_F1", resolution, tbShaderThreadSize);
        }
    }

// --- SET NOISE TEXTURE ---

    public void SetPerlin (ref RenderTexture texture, int3 resolution, int cellSize, int rngSeed)
    {
        // -- PERLIN_3D -> PerlinNoise --

        UpdateScriptTextures(resolution, cellSize);

        int NumPasses = (int)Mathf.Log(cellSize, 2);

        ngShader.SetInt("RngSeed", rngSeed);
        ngShader.SetInt("NumPasses", NumPasses);
        ngShader.SetInt("MaxNoiseCellSize", cellSize);

        int cellSizeIterator = cellSize*2;
        for (int pass = 0; pass < NumPasses; pass++)
        {
            cellSizeIterator /= 2;
            ngShader.SetInt("NoiseCellSize", cellSizeIterator);
            ngShader.SetInt("PassCount", pass);

            ComputeHelper.DispatchKernel(ngShader, "GenerateVectorMap", resolution / cellSizeIterator, ngShaderThreadSize);

            ngShader.SetTexture(1, "PerlinNoise", texture);
            ComputeHelper.DispatchKernel(ngShader, "Perlin", resolution, ngShaderThreadSize);
        }
    }

    public void SetVoronoi (ref RenderTexture texture, int3 resolution, int cellSize, int rngSeed)
    {
        // -- VORONOI_3D -> VoronoiNoise --

        UpdateScriptTextures(resolution, cellSize);

        ngShader.SetInt("RngSeed", rngSeed);
        ngShader.SetInt("MaxNoiseCellSize", cellSize);

        ComputeHelper.DispatchKernel(ngShader, "GenerateTrilinearsMap", resolution / cellSize, ngShaderThreadSize);
        ngShader.SetTexture(3, "VoronoiNoise", texture);
        ComputeHelper.DispatchKernel(ngShader, "Voronoi", resolution, ngShaderThreadSize);
    }

// --- CLASS ---

    public void UpdateScriptTextures (int3 newResolution, int newCellSize)
    {
        bool3 resolutionHasChanged = newResolution != LastResolution;
        bool cellSizeHasChanged = newCellSize != LastCellSize;
        bool settingsHasChanged = resolutionHasChanged.x || resolutionHasChanged.y || resolutionHasChanged.z || cellSizeHasChanged;

        if (!settingsHasChanged) { return; }

        T_VectorMap = CreateTexture(newResolution / newCellSize, 3);

        T_TrilinearsMap = CreateTexture(newResolution / newCellSize, 3);

        LastResolution = newResolution;
        LastCellSize = newCellSize;
    }
}