using UnityEngine;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Reflection;

[StructLayout(LayoutKind.Sequential)]
struct RigidBody
{
    public Vector2 pos;     // offset 0 (2 floats)
    public Vector2 vel;     // offset 2 (2 floats)
}

[StructLayout(LayoutKind.Sequential)]
struct VertexData
{
    public int parentIndex; // offset 0 (stored as float)
    public Vector2 pos;     // offset 1 (2 floats)
}

public class FragmentShaderBufferRenderer : MonoBehaviour
{
    public int maxTextureSize = 16;
    public int numRigidBodies = 10;
    public Material material;

    private Texture2D rigidBodyTex;
    private Texture2D vertexTex;
    
    private int numVertices;
    private int dataPerRigidBody;
    private int dataPerVertex;
    private RigidBody[] rigidBodyData;
    private VertexData[] vertexData;

    // State variables
    private bool paused = false;
    private float timeElapsed = 0;
    private float zoom = 0.25f;

    private static int GetStride<T>() => Marshal.SizeOf(typeof(T));
    private static int GetSize<T>() => GetStride<T>() / 4;

    float[] Flatten<T>(T item)
    {
        List<float> values = new List<float>();
        foreach (FieldInfo field in typeof(T).GetFields())
        {
            object val = field.GetValue(item);
            if (field.FieldType == typeof(float))
                values.Add((float)val);
            else if (field.FieldType == typeof(int))
                values.Add((int)val);
            else if (field.FieldType == typeof(Vector2))
            {
                Vector2 vec = (Vector2)val;
                values.Add(vec.x);
                values.Add(vec.y);
            }
            else if (field.FieldType == typeof(Vector3))
            {
                Vector3 vec = (Vector3)val;
                values.Add(vec.x);
                values.Add(vec.y);
                values.Add(vec.z);
            }
            else if (field.FieldType == typeof(Vector4))
            {
                Vector4 vec = (Vector4)val;
                values.Add(vec.x);
                values.Add(vec.y);
                values.Add(vec.z);
                values.Add(vec.w);
            }
        }
        return values.ToArray();
    }

    void Start()
    {
        // Compute float slots per struct.
        dataPerRigidBody = GetSize<RigidBody>();
        dataPerVertex = GetSize<VertexData>();

        // Rigid Bodies
        rigidBodyData = new RigidBody[numRigidBodies];

        // Vertices
        numVertices = numRigidBodies * 4;
        vertexData = new VertexData[numVertices];
        for (int i = 0; i < numVertices; i++)
        {
            int parent = i / 4;
            vertexData[i] = new VertexData
            {
                parentIndex = parent,
                pos = GetSquareVerts(i % 4)
            };
        }

        // Data texture setup for rigid bodies
        int totalRigidSlots = numRigidBodies * dataPerRigidBody;
        int squareSizeRigid = Mathf.CeilToInt(Mathf.Sqrt(totalRigidSlots));
        if (squareSizeRigid > maxTextureSize)
        {
            Debug.LogWarning("Rigid body slots needed higher than maximum data texture size: " + totalRigidSlots + " req / " + (maxTextureSize * maxTextureSize) + " sup");
            squareSizeRigid = maxTextureSize;
        }
        Debug.Log("Rigid body texture created with dimensions: " + squareSizeRigid + "x" + squareSizeRigid + " (" + (squareSizeRigid * squareSizeRigid) + " slots)");
        rigidBodyTex = new Texture2D(squareSizeRigid, squareSizeRigid, TextureFormat.RFloat, false);

        // Data texture setup for vertices
        int totalVertexSlots = numVertices * dataPerVertex;
        int squareSizeVertex = Mathf.CeilToInt(Mathf.Sqrt(totalVertexSlots));
        if (squareSizeVertex > maxTextureSize)
        {
            Debug.LogWarning("Vertex slots needed higher than maximum data texture size: " + totalVertexSlots + " req / " + (maxTextureSize * maxTextureSize) + " sup");
            squareSizeVertex = maxTextureSize;
        }
        Debug.Log("Vertex texture created with dimensions: " + squareSizeVertex + "x" + squareSizeVertex + " (" + (squareSizeVertex * squareSizeVertex) + " slots)");
        vertexTex = new Texture2D(squareSizeVertex, squareSizeVertex, TextureFormat.RFloat, false);

        // Update shader data
        UpdateShaderData();
    }

    Vector2 GetSquareVerts(int index)
    {
        float halfSide = 0.05f;
        return index switch
        {
            1 => new Vector2(halfSide, -halfSide),
            2 => new Vector2(halfSide, halfSide),
            3 => new Vector2(-halfSide, halfSide),
            _ => new Vector2(-halfSide, -halfSide),
        };
    }

    void Update()
    {
        HandleKeyInputs();
        
        if (paused) return;

        RunSimulationStep(Time.deltaTime);
        UpdateShaderData();
    }

    void HandleKeyInputs()
    {
        if (Input.GetKeyDown(KeyCode.Space)) paused = !paused;

        if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus))
        {
            zoom += 0.5f * Time.unscaledDeltaTime;
        }
        if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus))
        {
            zoom = Mathf.Max(zoom - 0.5f * Time.unscaledDeltaTime, 0.1f);
        }
    }

    void RunSimulationStep(float deltaTime)
    {
        timeElapsed += deltaTime;

        // Update rigid body positions.
        for (int i = 0; i < numRigidBodies; i++)
        {
            float t = timeElapsed + i * 0.5f;
            rigidBodyData[i] = new RigidBody
            {
                pos = new Vector2(Mathf.Sin(t), Mathf.Cos(t)) + Vector2.one,
                vel = new Vector2(Mathf.Cos(t), Mathf.Sin(t))
            };
        }
    }

    void UpdateShaderData()
    {
        // Update rigid body texture
        int rigidWidth = rigidBodyTex.width;
        int rigidHeight = rigidBodyTex.height;
        int totalRigidPixels = rigidWidth * rigidHeight;
        Color[] rigidPixels = new Color[totalRigidPixels];
        for (int i = 0; i < totalRigidPixels; i++)
        {
            rigidPixels[i] = new Color(0, 0, 0, 1);
        }
        int rigidFlatIndex = 0;
        for (int i = 0; i < numRigidBodies; i++)
        {
            float[] values = Flatten(rigidBodyData[i]);
            for (int j = 0; j < values.Length; j++)
            {
                if (rigidFlatIndex < totalRigidPixels)
                {
                    rigidPixels[rigidFlatIndex] = new Color(values[j], 0, 0, 1);
                    rigidFlatIndex++;
                }
            }
        }
        rigidBodyTex.SetPixels(rigidPixels);
        rigidBodyTex.Apply();

        // Update vertex texture
        int vertexWidth = vertexTex.width;
        int vertexHeight = vertexTex.height;
        int totalVertexPixels = vertexWidth * vertexHeight;
        Color[] vertexPixels = new Color[totalVertexPixels];
        for (int i = 0; i < totalVertexPixels; i++)
        {
            vertexPixels[i] = new Color(0, 0, 0, 1);
        }
        int vertexFlatIndex = 0;
        for (int i = 0; i < numVertices; i++)
        {
            float[] values = Flatten(vertexData[i]);
            for (int j = 0; j < values.Length; j++)
            {
                if (vertexFlatIndex < totalVertexPixels)
                {
                    vertexPixels[vertexFlatIndex] = new Color(values[j], 0, 0, 1);
                    vertexFlatIndex++;
                }
            }
        }
        vertexTex.SetPixels(vertexPixels);
        vertexTex.Apply();

        material.SetTexture("_RigidBodyData", rigidBodyTex);
        material.SetTexture("_VertexData", vertexTex);
        material.SetInt("_NumRigidBodies", numRigidBodies);
        material.SetInt("_NumVertexElements", numVertices);
        material.SetInt("_RigidBodyTexWidth", rigidBodyTex.width);
        material.SetInt("_RigidBodyTexHeight", rigidBodyTex.height);
        material.SetFloat("_RigidBodyInvTexWidth", 1.0f / rigidBodyTex.width);
        material.SetFloat("_RigidBodyInvTexHeight", 1.0f / rigidBodyTex.height);
        material.SetInt("_VertexTexWidth", vertexTex.width);
        material.SetInt("_VertexTexHeight", vertexTex.height);
        material.SetFloat("_VertexInvTexWidth", 1.0f / vertexTex.width);
        material.SetFloat("_VertexInvTexHeight", 1.0f / vertexTex.height);
        material.SetFloat("_Zoom", zoom);
        // Pass the computed number of floats per element from C#
        material.SetInt("_RigidFloatsPerElement", dataPerRigidBody);
        material.SetInt("_VertexFloatsPerElement", dataPerVertex);
    }
}