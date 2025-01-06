using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using Unity.Mathematics;

[RequireComponent(typeof(PolygonCollider2D))]
public class FluidSpawner : Polygon
{
    public bool DoDrawBody = true;
    [Range(50.0f, 2000.0f)] public float msSpawnInterval;
    [SerializeField] private int pTypeIndex;
    [SerializeField] private Vector2 velocity;
    [SerializeField] private float tempCelcius;
    [SerializeField] private float spawnDensity;
    [SerializeField] private int maxSpawnedParticlesPerUpdate;

    private SceneManager sceneManager;
    private PTypeInput pTypeInput;
    private Main main;
    private List<Vector2> generatedPoints;

#if UNITY_EDITOR
    public override void OnEditorUpdate() {} // Not used
#endif

    public PData[] GenerateParticles()
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        if (pTypeInput == null) pTypeInput = GameObject.FindGameObjectWithTag("PTypeInput").GetComponent<PTypeInput>();

        if (generatedPoints == null)
        {
            SetPolygonData();
            generatedPoints = GeneratePoints();
        }

        // Validate pTypeIndex
        if (pTypeIndex >= pTypeInput.particleTypeStates.Length * 3)
            Debug.LogError("pTypeIndex outside valid range. FluidSpawner: " + this.name);

        PData[] pDatas = new PData[generatedPoints.Count];
        for (int i = 0; i < pDatas.Length; i++)
        {
            pDatas[i] = InitPData(generatedPoints[i]);
        }
        return pDatas;
    }

    private List<Vector2> GeneratePoints()
    {
        if (sceneManager == null)
            sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        List<Vector2> points = new();

        Vector2 min = Func.MinVector2(Edges.Select(e => Func.MinVector2(e.start, e.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(e => Func.MaxVector2(e.start, e.end)).ToArray());

        int iterationCount = 0;
        for (float x = min.x; x <= max.x; x += spawnDensity)
        {
            for (float y = min.y; y <= max.y; y += spawnDensity)
            {
                Vector2 pt = new(x, y);

                if (IsPointInsidePolygon(pt) && sceneManager.IsPointInsideBounds(pt))
                {
                    if (++iterationCount > maxSpawnedParticlesPerUpdate)
                        return points;

                    points.Add(pt);
                }
            }
        }

        return points;
    }

    private PData InitPData(Vector2 pos)
    {
        return new PData
        {
            predPos = new float2(0.0f, 0.0f),
            pos = pos,
            vel = velocity,
            lastVel = new float2(0.0f, 0.0f),
            density = 0.0f,
            nearDensity = 0.0f,
            temperature = Utils.CelsiusToKelvin(tempCelcius),
            temperatureExchangeBuffer = 0.0f,
            lastChunkKey_PType_POrder = pTypeIndex * main.ChunksNumAll
        };
    }
}
