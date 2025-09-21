using UnityEngine;
using UnityEngine.Experimental.Rendering;

public class DemoComputeShader : MonoBehaviour
{
    // Settings
    public int framesPerZ;
    public int upscaleFactor;
    public bool use_true2DArray_false3D;

    // Texture settings
    public Vector3Int outputDims;
    
    // References
    public ComputeShader computeShader;
    public Texture2DArray my2DArray;
    public Texture3D my3D;

    // Result of the compute shader
    private RenderTexture outputTex;
    private int kernel2D;
    private int kernel3D;

    private int frameCount = 0;

    void Start()
    {
        outputTex = new(outputDims.x, outputDims.y, 0, GraphicsFormat.R8G8B8A8_UNorm)
        {
            enableRandomWrite = true
        };
        outputTex.Create();

        if (use_true2DArray_false3D)
        {
            kernel2D = computeShader.FindKernel("CS_Sample2DArray");
            computeShader.SetTexture(kernel2D, "_MyTex2DArray", my2DArray);
            computeShader.SetTexture(kernel2D, "_Output", outputTex);
        }
        else
        {
            kernel3D = computeShader.FindKernel("CS_Sample3D");
            computeShader.SetTexture(kernel3D, "_MyTex3D", my3D);
            computeShader.SetTexture(kernel3D, "_Output", outputTex);
        }
    }

    private void Update()
    {
        frameCount++;
        computeShader.SetInt("zIndex", (int)(frameCount / framesPerZ) % outputDims.z);
        computeShader.SetInt("texSize", outputDims.x);
        computeShader.SetInt("upscaleFactor", upscaleFactor);

        if (use_true2DArray_false3D)
        {
            computeShader.Dispatch(kernel2D, outputDims.x / 8, outputDims.y / 8, 1);
        }
        else
        {
            computeShader.Dispatch(kernel3D, outputDims.x / 8, outputDims.x / 8, 1);
        }
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest) => Graphics.Blit(outputTex, dest);
}