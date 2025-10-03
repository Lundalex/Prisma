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
    [Range(0.0f, 1.0f)] public float particleOffsetMagnitude = 0.1f;
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
        List<Vector2> generatedPoints = GeneratePoints(true, gridSpacing);

        if (pTypeIndex >= pTypeInput.particleTypeStates.Length * 3) Debug.LogError("pTypeIndex outside valid range. SceneFluid: " + this.name);

        PData[] pDatas = new PData[generatedPoints.Count];
        for (int i = 0; i < pDatas.Length; i++)
            pDatas[i] = InitPData(generatedPoints[i] + pointOffset, particleTemperatureCelcius);

        return pDatas;
    }

    public List<Vector2> GeneratePoints(bool doApplySmallRandOffset, float gridSpacing = 0)
    {
        if (sceneManager == null) sceneManager = GameObject.Find("SceneManager").GetComponent<SceneManager>();

        bool editorView = gridSpacing == -1;
        if (editorView) gridSpacing = editorGridSpacing;
        else if (gridSpacing == 0) gridSpacing = defaultGridSpacing;

        SceneRigidBody[] allRigidBodies = SceneManager.GetAllSceneRigidBodies();
        SceneFluid[] allFluids = SceneManager.GetAllSceneFluids();

        // Precompute polygon AABB (avoid LINQ/allocs)
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var e in Edges)
        {
            if (e.start.x < min.x) min.x = e.start.x;
            if (e.start.y < min.y) min.y = e.start.y;
            if (e.end.x   < min.x) min.x = e.end.x;
            if (e.end.y   < min.y) min.y = e.end.y;

            if (e.start.x > max.x) max.x = e.start.x;
            if (e.start.y > max.y) max.y = e.start.y;
            if (e.end.x   > max.x) max.x = e.end.x;
            if (e.end.y   > max.y) max.y = e.end.y;
        }

        // Quantise just like before (kept behaviour)
        min.x -= min.x % editorGridSpacing;
        min.y -= min.y % editorGridSpacing;
        max.x += max.x % editorGridSpacing;
        max.y += max.y % editorGridSpacing;

        // NEW: get scene bounds once, do a cheap rect test per point
        sceneManager.GetSceneBounds(out Vector2 sMin, out Vector2 sMax);

        int iterationCount = 0;
        List<Vector2> generatedPoints = new();
        for (float x = min.x; x <= max.x; x += gridSpacing)
        {
            for (float y = min.y; y <= max.y; y += gridSpacing)
            {
                Vector2 point = new(x, y);
                if (doApplySmallRandOffset) point += SmallRandVector2(particleOffsetMagnitude * gridSpacing);

                // Fast reject: scene bounds
                if (point.x <= sMin.x || point.y <= sMin.y || point.x >= sMax.x || point.y >= sMax.y)
                    continue;

                if (IsPointInsidePolygon(point) &&
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
            lastChunkKey_PType_POrder = pTypeIndex * (main != null ? main.ChunksNumAll : 0),
            temperature = Utils.CelsiusToKelvin(tempCelsius),
            temperatureExchangeBuffer = 0.0f
        };
    }

    private static Vector2 SmallRandVector2(float range)
    {
        float x = UnityEngine.Random.Range(-range, range);
        float y = UnityEngine.Random.Range(-range, range);
        return new Vector2(x, y);
    }
}