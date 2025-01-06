using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Render : MonoBehaviour
{
    public GameObject Sim;
    public Simulation_MultiCore MainScript;
    public ComputeShader computeShader;
    public RenderTexture renderTexture;
    private ComputeBuffer ParticlePositionBuffer;
    private ComputeBuffer ChunkBuffer;

    void Start()
    {
        MainScript = Sim.GetComponent<Simulation_MultiCore>();

        ParticlePositionBuffer = new ComputeBuffer(MainScript.particles_num, sizeof(float) * 2);
        int ChunkBufferTotNum = MainScript.border_width / MainScript.Lg_chunk_dims * MainScript.border_height / MainScript.Lg_chunk_dims * MainScript.Lg_chunk_capacity;
        ChunkBuffer = new ComputeBuffer(ChunkBufferTotNum, sizeof(int));

        renderTexture = new RenderTexture(800, 400, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (MainScript.Render_with_shader == false) 
        {
            Graphics.Blit(src, dest); // Just pass the original image through
            return;
        }

        // Set the compute shader variables
        computeShader.SetFloat("Radius", 0.3f);
        computeShader.SetInt("NumberOfCircles", MainScript.particles_num);
        computeShader.SetInt("ResolutionWidth", renderTexture.width);
        computeShader.SetInt("ResolutionHeight", renderTexture.height);
        computeShader.SetInt("MaxW", MainScript.border_width);
        computeShader.SetInt("MaxH", MainScript.border_height);
        computeShader.SetInt("ChunkCapacity", MainScript.Lg_chunk_capacity);
        computeShader.SetInt("ChunkNumX", MainScript.Lg_chunk_amount_x);
        computeShader.SetInt("ChunkNumY", MainScript.Lg_chunk_amount_y);
        computeShader.SetInt("ChunkDims", MainScript.Lg_chunk_dims);

        // For the StructuredBuffer, you need to create a ComputeBuffer
        ParticlePositionBuffer.SetData(MainScript.position);
        ChunkBuffer.SetData(MainScript.lg_particle_chunks);
        computeShader.SetBuffer(0, "Positions", ParticlePositionBuffer);
        computeShader.SetBuffer(0, "Chunks", ChunkBuffer);

        computeShader.SetTexture(0, "Result", renderTexture);
        computeShader.Dispatch(0, renderTexture.width / 8, renderTexture.height / 8, 1);

        Graphics.Blit(renderTexture, dest);
    }
}
