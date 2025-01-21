using UnityEngine;
using System.Collections.Generic;
using System;
using Resources2;
using System.Linq;
using UnityEditor;
using Unity.Mathematics;
using PM = ProgramManager;

[RequireComponent(typeof(PolygonCollider2D))]
public class SceneFluid : Polygon
{
    public bool DoCenterPosition = false;
    public EditorRenderMethod editorRenderMethod;
    public int MaxGizmosIterations = 20000;
    [Range(0.1f, 10.0f)] public float editorGridSpacing = 0.5f;
    [Range(0.05f, 2.0f)] public float editorPointRadius = 0.05f;

    [Header("Simulation Object Settings")]
    [Range(0.1f, 10.0f)] public float defaultGridSpacing = 2.0f;
    [SerializeField] private float particleTemperatureCelcius = 20.0f;
    [SerializeField] private int pTypeIndex = 0;

    [Header("Preview Values")]
    [NonSerialized] public Vector2[] Points;
    private SceneManager sceneManager;
    private PTypeInput pTypeInput;
    private Main main;

#if UNITY_EDITOR
    // Editor
    private int framesSinceLastPositionChange = 0;
    private Vector2 lastFramePosition = Vector2.zero;

    public override void OnEditorUpdate()
    {
        if (Application.isPlaying) return;

        // Skip re‑assigning collider points if user is actively dragging handles
        bool userIsModifying = Tools.current == Tool.Move || Tools.current == Tool.Rotate || Tools.current == Tool.Scale;
        if (userIsModifying) return;

        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();

        if (!Application.isPlaying)
        {
            if (lastFramePosition.x != transform.position.x || lastFramePosition.y != transform.position.y)
            {
                lastFramePosition = transform.position;
                framesSinceLastPositionChange = 0;
            }
            else framesSinceLastPositionChange++;
            if (framesSinceLastPositionChange < 10) return;

            if (DoCenterPosition)
            {
                CenterPolygonPosition();
                DoCenterPosition = false;
            }
            
            if (snapPointToGrid) SnapPointsToGrid();
        }
    }
#endif

    private void OnValidate() => PM.Instance.doOnSettingsChanged = true;
    
    public PData[] GenerateParticles(Vector2 pointOffset, float gridSpacing = 0)
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        if (pTypeInput == null) pTypeInput = GameObject.FindGameObjectWithTag("PTypeInput").GetComponent<PTypeInput>();

        SetPolygonData();
        List<Vector2> generatedPoints = GeneratePoints(gridSpacing);

        if (pTypeIndex >= pTypeInput.particleTypeStates.Length * 3) Debug.LogError("pTypeIndex outside valid range. SceneFluid: " + this.name);

        PData[] pDatas = new PData[generatedPoints.Count];
        for (int i = 0; i < pDatas.Length; i++)
        {
            pDatas[i] = InitPData(generatedPoints[i] + pointOffset, particleTemperatureCelcius);
        }

        return pDatas;
    }

    public List<Vector2> GeneratePoints(float gridSpacing = 0)
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        bool editorView = gridSpacing == -1;
        if (editorView) gridSpacing = editorGridSpacing;
        else if (gridSpacing == 0) gridSpacing = defaultGridSpacing;

        SceneRigidBody[] allRigidBodies = SceneManager.GetAllSceneRigidBodies();
        SceneFluid[] allFluids = SceneManager.GetAllSceneFluids();

        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        min.x -= min.x % editorGridSpacing;
        min.y -= min.y % editorGridSpacing;
        max.x += max.x % editorGridSpacing;
        max.y += max.y % editorGridSpacing;

        int iterationCount = 0;
        List<Vector2> generatedPoints = new();
        for (float x = min.x; x <= max.x; x += gridSpacing)
        {
            for (float y = min.y; y <= max.y; y += gridSpacing)
            {
                // Offset the spawning of each particle slightly to avoid visual rendering artifacts the first few frames
                Vector2 point = new Vector2(x, y);

                if (IsPointInsidePolygon(point) &&
                    sceneManager.IsPointInsideBounds(point) &&
                    sceneManager.IsSpaceEmpty(point, this, allRigidBodies, allFluids))
                {
                    if (++iterationCount > MaxGizmosIterations && editorView) return generatedPoints;

                    generatedPoints.Add(point);
                }
            }
        }

        return generatedPoints;
    }

    private PData InitPData(Vector2 pos, float tempCelsius)
    {
        return new PData
        {
            predPos = new float2(0.0f, 0.0f),
            pos = pos,
            vel = new float2(0, 0),
            lastVel = new float2(0.0f, 0.0f),
            density = 0.0f,
            nearDensity = 0.0f,
            temperature = Utils.CelsiusToKelvin(tempCelsius),
            temperatureExchangeBuffer = 0.0f,
            lastChunkKey_PType_POrder = pTypeIndex * main.ChunksNumAll
        };
    }

    private static Vector2 SmallRandVector2(float range)
    {
        // Randomize each component within the range
        float x = UnityEngine.Random.Range(-range, range);
        float y = UnityEngine.Random.Range(-range, range);

        return new Vector2(x, y);
    }
}