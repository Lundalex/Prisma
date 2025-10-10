using UnityEngine; 
using Unity.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Resources2;
using PM = ProgramManager;
using Debug = UnityEngine.Debug;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class Main : MonoBehaviour
{
#if UNITY_EDITOR
    [ContextMenu("Reset Defaults")]
    public void ResetDefaults()
    {
        LiquidF0 = 0.02f;
        LiquidReflectionStrength = 0.85f;
        LiquidRefractionStrength = 1.5f;
        LiquidSpecularStrength = 1.0f;
        LiquidShininess = 100.0f;
        LiquidDistortScales = new(0.03f, 0.015f);
        LiquidAbsorptionColor = new(0.0f, 0.25f, 0.35f);
        LiquidAbsorptionStrength = 1.2f;
        LiquidNormalZBias = 2f;
        LiquidSlopeThreshold = 1f;

        RBRoundLightStrength = 2f;
    }
#endif

    public SimulationDevice simDevice = SimulationDevice.GPU;

    #region Safety
    public float MaxPVel = 100;
    public float MaxRBRotVel = 100;
    public float MaxRBVel = 100;
    public float MinRBVelForMovement = 0.1f;
    #endregion

    #region Sensor Normalization
    public float SimUnitToMetersFactor = 0.005f;
    public float ZDepthMeters = 0.1f;
    public float PressureFactor = 1.77f;
    #endregion

    #region Fluid Simulation
    public float LookAheadTime = 0.017f;
    public float StateThresholdPadding = 3.0f;
    public int MaxInfluenceRadius = 2;
    [SerializeField] private int MaxParticlesNum = 30000;
    [SerializeField] private int MaxStartingParticlesNum = 20000;
    [SerializeField] private int MaxSpringsPerParticle = 150;
    #endregion

    #region Scene Boundary
    public int2 BoundaryDims = new(300, 200);
    public float FluidPadding = 4.0f;
    public float RigidBodyPadding = 2.0f;
    public float BoundaryElasticity = 0.2f;
    public float BoundaryFriction = 0.0f;
    #endregion

    #region Engine Optimisations
    public bool DoSimulateParticleViscosity = true;
    public bool DoSimulateParticleSprings = true;
    public bool DoSimulateParticleTemperature = true;

    public float FloatIntPrecisionRB = 50000.0f;
    public float FloatIntPrecisionRBRot = 500000.0f;
    public float FloatIntPrecisionP = 1000.0f;
    #endregion

    #region Rigid Body Simulation
    public bool AllowLinkedRBCollisions = false;
    public float RB_RBCollisionCorrectionFactor = 0.8f;
    public float RB_RBFixedCollisionCorrection = 0.05f;
    public float RB_RBRigidConstraintCorrectionFactor = 5.0f;
    #endregion

    #region Simulation Time
    public int TimeStepsPerFrame = 3;
    public int SubTimeStepsPerFrame = 3;
    public int SubTimeStepsPerRBSimUpdate = 1;
    public TimeStepType TimeStepType;
    public int TargetFrameRate;
    public float TimeStep = 0.02f;
    public float ProgramSpeed = 2.0f;
    #endregion

    #region Mouse Interaction
    public float MaxInteractionRadius = 40.0f;
    public float InteractionAttractionPower = 3.5f;
    public float InteractionRepulsionPower = 3.5f;
    public float InteractionFountainPower = 1.0f;
    public float InteractionTemperaturePower = 1.0f;
    public float InteractionDampening = 1.0f;

    public float RB_MaxInteractionRadius = 40.0f;
    public float RB_InteractionAttractionPower = 3.5f;
    public float RB_InteractionRepulsionPower = 3.5f;
    public float RB_InteractionDampening = 0.1f;
    #endregion

    #region Render Pipeline
    public FluidRenderMethod FluidRenderMethod;
    public SampleMethod SampleMethod;
    public CausticsType CausticsTypeEditor;
    public CausticsType CausticsTypeBuild;
    public bool DoDrawFluidOutlines;
    public bool DoDisplayFluidVelocities;
    public bool DoDrawUnoccupiedFluidSensorArea;
    public bool DoDrawRBOutlines;
    public bool DoDrawRBCentroids;
    public bool DoUseFastShaderCompilation;

    public List<RenderStep> RenderOrder = new()
    {
        RenderStep.Background,
        RenderStep.Fluids,
        RenderStep.RigidBodies,
        RenderStep.RigidBodySprings,
        RenderStep.UI
    };
    #endregion

    #region Post Processing
    public ShadowType ShadowType;
    public float ShadowDarkness = 0.45f;
    public float ShadowFalloff = 0.01f;
    public float RBShadowStrength = 1.0f;
    public float LiquidShadowStrength = 0.0005f;
    public float GasShadowStrength = 0.00003f;
    public float ShadowDirection = 60f;
    public int ShadowBlurRadius = 1;
    public int ShadowBlurIterations = 1;
    public float ShadowDiffusion = 20.0f;

    public bool DoUseRimLighting = true;
    public float RimShadingStrength = 2.0f;
    public float RimShadingBleed = 0.2f;
    public float RimShadingOpaqueBleed = 3.0f;

    public bool DoUseAntiAliasing = true;
    public float AAThreshold = 0.08f;
    public float AAMaxBlend = 0.75f;

    public int ShadowDownSampling = 1;
    #endregion

    #region Render Display
    public int2 DefaultResolution = new int2(1920, 1080);
    public ResolutionScale ResolutionScaleSetting = ResolutionScale.Scale_1_1;
    public enum ResolutionScale
    {
        Scale_2_1,
        Scale_1_1,
        Scale_3_4,
        Scale_2_3,
        Scale_1_2,
        Scale_1_3,
        Scale_1_4
    }

    public Vector2 UIPadding;

    public LightingSettings LightingSettings;
    public float3 GlobalBrightness;
    public float Contrast;
    public float Saturation;
    public float Gamma;
    public float SettingsViewDarkTintPercent;
    public float PauseDarkTintPercent;
    public float PrecomputedCausticsFPS;
    public float PrecomputedCausticsScaleFactor;
    public float DynamicCausticsScaleFactor;
    public float PrecomputedCausticsZBlurFactor;

    public float SpringRenderWidth;
    public float SpringRenderMatWidth;
    public float SpringRenderRodLength;
    public int SpringRenderNumPeriods;
    public float TaperThresoldNormalised = 0.2f;
    public float2 SpringTextureUVFactor = new(10.0f, 1.0f);

    public float LiquidMetaballsThreshold = 1.0f;
    public float LiquidMetaballsEdgeDensityWidth = 0.0f;
    public float VisualLiquidParticleRadius = 0.4f;
    public float LiquidEdgeWidth = 0.0f;

    public float  LiquidF0                   = 0.02f;
    public float  LiquidReflectionStrength   = 0.85f;
    public float  LiquidRefractionStrength   = 1.5f;
    public float  LiquidSpecularStrength     = 1.0f;
    public float  LiquidShininess            = 100.0f;
    public float2 LiquidDistortScales        = new(0.03f, 0.015f);
    public float3 LiquidAbsorptionColor      = new(0.0f, 0.25f, 0.35f);
    public float  LiquidAbsorptionStrength   = 1.2f;
    public float  LiquidNormalZBias          = 2f;
    public float  LiquidSlopeThreshold       = 1f;

    public Gradient LiquidVelocityGradient;
    public int LiquidVelocityGradientResolution = 256;
    public float LiquidVelocityGradientMaxValue = 60;

    public float GasMetaballsThreshold = 1.0f;
    public float GasMetaballsEdgeDensityWidth = 0.0f;
    public float VisualGasParticleRadius = 0.4f;
    public float GasEdgeWidth = 0.0f;
    public float GasNoiseStrength = 1.0f;
    public float GasNoiseDensityDarkeningFactor;
    public float GasNoiseDensityOpacityFactor;
    public float TimeSetRandInterval = 0.5f;

    public Gradient GasVelocityGradient;
    public int GasVelocityGradientResolution;
    public float GasVelocityGradientMaxValue;

    public float RBEdgeWidth = 0.0f;
    public float RBEdgeRoundDst = 2.0f;
    public float RBRoundLightStrength = 2.0f;
    public float RBRoundShadowStrength = 1.0f;
    public float RBRoundSamplePush = 1.0f;
    public float RBFalloff = 0.6f;

    public float MetalReflectionStrength = 0.35f;
    public float GlobalMatBrightnessMultiplier = 1.0f;

    public float FluidSensorEdgeWidth = 3.0f;
    public float SensorAreaAnimationSpeed = 2.0f;

    public float GlobalSettingsViewChangeSpeed;
    public float3 BackgroundBrightness;

    [Header("Background")]
    public CustomMat BackgroundCustomMat;

    [Header("Metal Highlights")]
    public CustomMat MetalHighlightsCustomMat;

    public static readonly float PathFlagOffset = 100000.0f;
    public static readonly float PathFlagThreshold = PathFlagOffset / 2.0f;
    #endregion

    #region References
    public RenderTexture uiTexture;
    public RenderTexture dynamicCausticsTexture;
    public Texture2DArray precomputedCausticsTexture;

    public PTypeInput pTypeInput;
    public SceneManager sceneManager;
    public ShaderHelper shaderHelper;
    public Transform fragmentTransform;

    public ComputeShader renderShader;
    public ComputeShader ppShader;
    public ComputeShader pSimShader;
    public ComputeShader rbSimShader;
    public ComputeShader sortShader;
    public ComputeShader debugShader;
    #endregion

    public ComputeBuffer SpatialLookupBuffer;
    public ComputeBuffer StartIndicesBuffer;

    public ComputeBuffer SpringCapacitiesBuffer;
    private bool FrameBufferCycle = true;
    public ComputeBuffer SpringStartIndicesBuffer_dbA;
    public ComputeBuffer SpringStartIndicesBuffer_dbB;
    public ComputeBuffer SpringStartIndicesBuffer_dbC;
    public ComputeBuffer ParticleSpringsCombinedBuffer;

    public ComputeBuffer PDataBuffer;
    public ComputeBuffer PTypeBuffer;
    public ComputeBuffer RecordedFluidDataBuffer;

    public ComputeBuffer RBVectorBuffer;
    public ComputeBuffer RBDataBuffer;
    public ComputeBuffer RBAdjustmentBuffer;

    public ComputeBuffer SensorAreaBuffer;
    public ComputeBuffer MaterialBuffer;

    public ComputeBuffer ShadowSrcFullRes;
    public ComputeBuffer ShadowMask_dbA;
    public ComputeBuffer ShadowMask_dbB;
    public ComputeBuffer SharpShadowMask;
    public ComputeBuffer ShadowDstMask;
    public ComputeBuffer RimLightMask;

    [NonSerialized] public int ParticlesNum;
    [NonSerialized] public int MaxInfluenceRadiusSqr;
    [NonSerialized] public float InvMaxInfluenceRadius;
    [NonSerialized] public int2 ChunksNum;
    [NonSerialized] public int ChunksNumAll;
    [NonSerialized] public int ParticleSpringsCombinedHalfLength;
    [NonSerialized] public int ParticlesNum_NextPow2;
    [NonSerialized] public int ParticlesNum_NextLog2;
    [NonSerialized] public int PTypesNum;
    [NonSerialized] public int NumRigidBodies;
    [NonSerialized] public int NumRigidBodyVectors;
    [NonSerialized] public int NumFluidSensors;
    [NonSerialized] public CausticsType CausticsType;
    [NonSerialized] public int3 PrecomputedCausticsDims;

    public const int renderShaderThreadSize = 16;
    public const int ppShaderThreadSize1 = 32;
    public const int ppShaderThreadSize2 = 16;
    public const int pSimShaderThreadSize1 = 512;
    public const int pSimShaderThreadSize2 = 512;
    public const int sortShaderThreadSize = 512;
    public const int rbSimShaderThreadSize1 = 64;
    public const int rbSimShaderThreadSize2 = 32;
    public const int rbSimShaderThreadSize3 = 512;

    [NonSerialized] public RenderTexture renderTexture;
    [NonSerialized] public RenderTexture ppRenderTexture;
    [NonSerialized] public Texture2D AtlasTexture;
    [NonSerialized] public Texture2D LiquidVelocityGradientTexture;
    [NonSerialized] public Texture2D GasVelocityGradientTexture;
    [NonSerialized] public GameObject causticsGen;

    private List<PData> NewPDatas = new();

    private Mat[] Mats;
    private List<CustomMat> _stableMatOrder = new();

    private float DeltaTime;
    private float RLDeltaTime;
    private float SimTimeElapsed;
    private int StepCount = 0;
    private int timeSetRand;
    private bool gpuDataSorted = false;
    [NonSerialized] public static bool2 MousePressed = false;

    private AsyncOperationHandle<Texture2DArray> _causticsHandle;
    private Coroutine _causticsCoroutine;
    private int _causticsLoadVersion = 0; // increments to cancel stale coroutines

    private int2 _cachedShadowRes = int2.zero;

    public int MaterialsCount => Mats != null ? Mats.Length : 0;
    [NonSerialized] public int BackgroundMatIndex = -1;
    [NonSerialized] public int MetalHighlightsMatIndex = -1;
    private bool causticsLoaded = false;
    [NonSerialized] public bool ppFrameValid = false;

    [NonSerialized] public Dictionary<CustomMat, int> MatIndexMap = new();

    public void SubmitParticlesToSimulation(PData[] particlesToAdd) => NewPDatas.AddRange(particlesToAdd);

    public void StartScript()
    {
        SimTimeElapsed = 0;

        ValidateHardwareCompatibility();
        RenderSetup();

        BoundaryDims = sceneManager.GetBounds(MaxInfluenceRadius);
        ChunksNum = BoundaryDims / MaxInfluenceRadius;
        ChunksNumAll = ChunksNum.x * ChunksNum.y;

        BuildAtlasAndMats();

        PData[] PDatas = sceneManager.GenerateParticles(MaxStartingParticlesNum);
        ParticlesNum = PDatas.Length;

        (RBData[] RBDatas, RBVector[] RBVectors, SensorArea[] SensorAreas) = sceneManager.CreateRigidBodies();
        NumRigidBodies = RBDatas.Length;
        NumRigidBodyVectors = RBVectors.Length;
        NumFluidSensors = SensorAreas.Length;

        TextureHelper.TextureFromGradient(ref LiquidVelocityGradientTexture, LiquidVelocityGradientResolution, LiquidVelocityGradient);
        TextureHelper.TextureFromGradient(ref GasVelocityGradientTexture, GasVelocityGradientResolution, GasVelocityGradient);

        SetConstants();
        InitTimeSetRand();
        SetLightingSettings();

        InitializeBuffers(PDatas, RBDatas, RBVectors, SensorAreas);
        renderTexture = TextureHelper.CreateTexture(PM.Instance.ResolutionInt2, 3);
        ppRenderTexture = TextureHelper.CreateTexture(PM.Instance.ResolutionInt2, 3);

        ComputeHelper.CreateStructuredBuffer<float>(ref ShadowSrcFullRes, renderTexture.width * renderTexture.height);

        AllocateOrResizeShadowWorkingBuffers();

        shaderHelper.SetPSimShaderBuffers(pSimShader);
        shaderHelper.SetRBSimShaderBuffers(rbSimShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);
        shaderHelper.SetRenderShaderTextures(renderShader);
        shaderHelper.SetPostProcessorBuffers(ppShader);
        shaderHelper.SetPostProcessorTextures(ppShader);
        shaderHelper.SetSortShaderBuffers(sortShader);

        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateRBSimShaderVariables(rbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.SetPostProcessorVariables(ppShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);

        SetShaderKeywords();
        InitCausticsGen();

#if UNITY_EDITOR
        ComputeShaderDebugger.CheckShaderConstants(this, debugShader, pTypeInput);
#endif

        UpdateShaderTimeStep();
        GPUSortChunkLookUp();
        GPUSortSpringLookUp();
        PM.Instance.clampedDeltaTime = Const.SMALL_FLOAT;
        if (PM.Instance.globalBrightnessFactor < 0f)
            PM.Instance.globalBrightnessFactor = 1f;
        UpdateScript();

        // Dumb fix for rigid body data sometimes becoming corrupted on start. This line just sets the buffer data again.
        RBDataBuffer.SetData(RBDatas);

        StringUtils.LogIfInEditor("Simulation started with " + ParticlesNum + " particles, " + NumRigidBodies + " rigid bodies, and " + NumRigidBodyVectors + " vertices. Platform: " + Application.platform);

        TryLoadAddressableCaustics();
    }

    public void UpdateScript()
    {
        UpdateSimulationPDatas();

        DeltaTime = GetDeltaTime(PM.Instance.clampedDeltaTime, true);
        RLDeltaTime = GetDeltaTime(Time.deltaTime, false);

        gpuDataSorted = false;
        for (int i = 0; i < TimeStepsPerFrame; i++)
        {
            UpdateShaderTimeStep();

            RunGPUSorting();

            if (i == 0) RunRenderShader();

            for (int j = 0; j < SubTimeStepsPerFrame; j++)
            {
                pSimShader.SetBool("TransferSpringData", j == 0);

                RunPSimShader(j);

                if (StepCount % SubTimeStepsPerRBSimUpdate == 0)
                {
                    rbSimShader.SetFloat("DeltaTime", DeltaTime * SubTimeStepsPerRBSimUpdate);
                    rbSimShader.SetFloat("RLDeltaTime", RLDeltaTime * SubTimeStepsPerRBSimUpdate);
                    RunRbSimShader();
                }

                ComputeHelper.DispatchKernel(pSimShader, "UpdatePositions", ParticlesNum, pSimShaderThreadSize1);

                StepCount++;
                SimTimeElapsed += DeltaTime;
            }

            gpuDataSorted = false;
        }
    }

    public void RunGPUSorting()
    {
        if (!gpuDataSorted && ParticlesNum > 0)
        {
            GPUSortChunkLookUp();
            GPUSortSpringLookUp();
            gpuDataSorted = true;
        }
    }

    private void UpdateSimulationPDatas()
    {
        int particlesToAdd = NewPDatas.Count;
        int availableSpace = MaxParticlesNum - ParticlesNum;

        if (particlesToAdd > 0 && availableSpace > 0)
        {
            particlesToAdd = Mathf.Min(particlesToAdd, availableSpace);

            if (particlesToAdd > 0)
            {
                ParticlesNum += particlesToAdd;
                SetConstants();
                UpdateSettings();

                PDataBuffer.SetData(NewPDatas.ToArray(), 0, ParticlesNum - particlesToAdd, particlesToAdd);
            }

            NewPDatas = new();
        }
    }

    public void OnValidate() => PM.Instance.doOnSettingsChanged = true;

    public void OnSettingsChanged() => UpdateShaderData();

    private void UpdateShaderData()
    {
        SetLightingSettings();
        SetConstants();

        if (Application.isPlaying && PM.Instance.programStarted) BuildAtlasAndMats();
        TextureHelper.TextureFromGradient(ref LiquidVelocityGradientTexture, LiquidVelocityGradientResolution, LiquidVelocityGradient);
        TextureHelper.TextureFromGradient(ref GasVelocityGradientTexture, GasVelocityGradientResolution, GasVelocityGradient);

        RecreateOrUpdateMaterialBuffer();

        shaderHelper.SetRenderShaderTextures(renderShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);

        UpdateSettings();

        SetShaderKeywords();
        InitCausticsGen();

        if (AllocateOrResizeShadowWorkingBuffers())
        {
            shaderHelper.SetRenderShaderBuffers(renderShader);
            shaderHelper.SetPostProcessorBuffers(ppShader);
            shaderHelper.SetPostProcessorVariables(ppShader);
        }
    }

    private void InitCausticsGen()
    {
        if (causticsGen == null) causticsGen = GameObject.FindGameObjectWithTag("CausticsGenerator");
        causticsGen.SetActive(CausticsType == CausticsType.Dynamic);
    }

    public void UpdateSettings()
    {
        PTypeBuffer.SetData(pTypeInput.GetParticleTypesData(MatIndexMap));

        if (MaterialBuffer != null && MaterialBuffer.count == MaterialsCount)
            MaterialBuffer.SetData(Mats);

        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateRBSimShaderVariables(rbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.SetPostProcessorVariables(ppShader);
        shaderHelper.UpdateSortShaderVariables(sortShader);
    }

    public void UpdateShaderTimeStep()
    {
        Vector2 mouseSimPos = GetMousePosInSimSpace(false);

        bool2 currentMouseInputs = Utils.GetMousePressed();
        bool skipUpdatingMouseInputs = (currentMouseInputs.x && MousePressed.x) || (currentMouseInputs.y && MousePressed.y);
        if (!skipUpdatingMouseInputs)
        {
            bool disallowMouseInputs = PM.Instance.CheckAnyUIElementHovered() || PM.Instance.CheckAnySensorBeingMoved() || PM.Instance.isAnySensorSettingsViewActive;
            MousePressed = disallowMouseInputs ? false : currentMouseInputs;
        }

        pSimShader.SetFloat("DeltaTime", DeltaTime);
        pSimShader.SetFloat("RLDeltaTime", RLDeltaTime);
        pSimShader.SetVector("MousePos", mouseSimPos);
        pSimShader.SetBool("LMousePressed", MousePressed.x);
        pSimShader.SetBool("RMousePressed", MousePressed.y);

        rbSimShader.SetFloat("SimTimeElapsed", SimTimeElapsed);
        rbSimShader.SetVector("MousePos", mouseSimPos);
        rbSimShader.SetBool("LMousePressed", MousePressed.x);
        rbSimShader.SetBool("RMousePressed", MousePressed.y);
        renderShader.SetFloat("TotalScaledTimeElapsed", PM.Instance.totalScaledTimeElapsed);

        FrameBufferCycle = !FrameBufferCycle;
        sortShader.SetBool("FrameBufferCycle", FrameBufferCycle);
        pSimShader.SetBool("FrameBufferCycle", FrameBufferCycle);

        pSimShader.SetInt("StepCount", StepCount);
        pSimShader.SetInt("StepRand", Func.RandInt(0, 99999));
    }

    Vector2 factorAtStart = Vector2.positiveInfinity;
    public Vector2 GetMousePosInSimSpace(bool doApplyUITransform)
    {
        if (factorAtStart.x == float.PositiveInfinity) factorAtStart = PM.Instance.ScreenToViewFactorScene;

        Vector3 mousePosVector3 = Camera.main.ScreenToViewportPoint(Input.mousePosition);
        Vector2 mousePos = new(mousePosVector3.x, mousePosVector3.y);

        Vector2 normalisedMousePos = (mousePos - Const.Vector2Half) * (doApplyUITransform ? factorAtStart : Vector2.one) / PM.Instance.ScreenToViewFactorScene + Const.Vector2Half;
        Vector2 simSpacePos = normalisedMousePos * new Vector2(BoundaryDims.x, BoundaryDims.y);

        return simSpacePos;
    }
    public void SetScreenToViewFactor(Vector2 screenToviewFactor)
    {
        renderShader.SetVector("ScreenToViewFactor", screenToviewFactor);
    }

    private void SetShaderKeywords()
    {
        if (DoUseFastShaderCompilation)
        {
#if !UNITY_EDITOR
            Debug.LogWarning("Fast shader compilation enabled in build version. This may slightly decrease runtime performance");
#endif
        }

        if (DoDrawRBCentroids) renderShader.EnableKeyword("DRAW_RB_CENTROIDS");
        else renderShader.DisableKeyword("DRAW_RB_CENTROIDS");
        if (DoDrawFluidOutlines) renderShader.EnableKeyword("DRAW_FLUID_OUTLINES");
        else renderShader.DisableKeyword("DRAW_FLUID_OUTLINES");
        if (DoDisplayFluidVelocities) renderShader.EnableKeyword("DISPLAY_FLUID_VELOCITIES");
        else renderShader.DisableKeyword("DISPLAY_FLUID_VELOCITIES");
        if (CausticsType != CausticsType.None) renderShader.EnableKeyword("USE_CAUSTICS");
        else renderShader.DisableKeyword("USE_CAUSTICS");
        if (CausticsType == CausticsType.Dynamic) renderShader.EnableKeyword("USE_DYNAMIC_CAUSTICS");
        else renderShader.DisableKeyword("USE_DYNAMIC_CAUSTICS");
        if (DoDrawUnoccupiedFluidSensorArea) renderShader.EnableKeyword("DRAW_UNOCCUPIED_FLUID_SENSOR_AREA");
        else renderShader.DisableKeyword("DRAW_UNOCCUPIED_FLUID_SENSOR_AREA");
        if (DoDrawRBOutlines) renderShader.EnableKeyword("DRAW_RB_OUTLINES");
        else renderShader.DisableKeyword("DRAW_RB_OUTLINES");
        if (FluidRenderMethod == FluidRenderMethod.Metaballs) renderShader.EnableKeyword("USE_METABALLS");
        else renderShader.DisableKeyword("USE_METABALLS");
        if (SampleMethod == SampleMethod.Bilinear) renderShader.EnableKeyword("USE_BILINEAR_SAMPLER");
        else renderShader.DisableKeyword("USE_BILINEAR_SAMPLER");
        if (DoUseFastShaderCompilation) renderShader.EnableKeyword("DO_USE_FAST_COMPILATION");
        else renderShader.DisableKeyword("DO_USE_FAST_COMPILATION");

        if (DoSimulateParticleViscosity) pSimShader.EnableKeyword("SIMULATE_PARTICLE_VISCOSITY");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_VISCOSITY");
        if (DoSimulateParticleSprings) pSimShader.EnableKeyword("SIMULATE_PARTICLE_SPRINGS");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_SPRINGS");
        if (DoSimulateParticleTemperature) pSimShader.EnableKeyword("SIMULATE_PARTICLE_TEMPERATURE");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_TEMPERATURE");

        if (DoUseFastShaderCompilation) rbSimShader.EnableKeyword("DO_USE_FAST_COMPILATION");
        else rbSimShader.DisableKeyword("DO_USE_FAST_COMPILATION");

        if (DoUseRimLighting) ppShader.EnableKeyword("APPLY_RIM_LIGHTING");
        else ppShader.DisableKeyword("APPLY_RIM_LIGHTING");
    }

    private void ValidateHardwareCompatibility()
    {
    }

    private void RenderSetup()
    {
        Camera.main.transform.position = new Vector3(BoundaryDims.x / 2, BoundaryDims.y / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(BoundaryDims.x * 0.75f, BoundaryDims.y * 1.5f);

        if (fragmentTransform != null)
        {
            fragmentTransform.position = new Vector3(BoundaryDims.x / 2, BoundaryDims.y / 2, -0.5f);
            fragmentTransform.localScale = 0.5f * Func.Int2ToVector2(PM.Instance.ResolutionInt2);
            fragmentTransform.gameObject.SetActive(simDevice != SimulationDevice.GPU);
        }
    }

    private void SetLightingSettings()
    {
        if (LightingSettings == LightingSettings.Custom) return;

        GlobalBrightness = 1.0f;
        Contrast = 1.0f;
        Saturation = 1.0f;
        Gamma = 1.0f;
        SettingsViewDarkTintPercent = 0.70f;
        PauseDarkTintPercent = 0.7f;
    }

    private float GetDeltaTime(float totalFrameTime, bool doClamp)
    {
        int stepsPerFrame = GetTimeStepsPerFrame();
        float deltaTime;

        if (TimeStepType == TimeStepType.Fixed)
        {
            deltaTime = TimeStep / stepsPerFrame;
            deltaTime *= PM.Instance.timeScale * ProgramSpeed;

        }
        else
        {
            deltaTime = totalFrameTime / stepsPerFrame;
            deltaTime *= PM.Instance.timeScale * ProgramSpeed;
            if (doClamp)
            {
                float newDeltaTime = Mathf.Min(deltaTime, TimeStep);
                PM.Instance.scaledDeltaTime *= (newDeltaTime / deltaTime);
                deltaTime = newDeltaTime;
            }
        }

        if (PM.Instance.slowMotionActive) deltaTime /= PM.SlowMotionFactor;

        return deltaTime;
    }

    public int GetTimeStepsPerFrame() => TimeStepsPerFrame * SubTimeStepsPerFrame;

    private void SetConstants()
    {
        MaxInfluenceRadiusSqr = MaxInfluenceRadius * MaxInfluenceRadius;
        InvMaxInfluenceRadius = 1.0f / MaxInfluenceRadius;
        ParticleSpringsCombinedHalfLength = MaxParticlesNum * MaxSpringsPerParticle / 2;
        ParticlesNum_NextPow2 = Func.NextPow2(MaxParticlesNum);
        ParticlesNum_NextLog2 = (int)Math.Log(ParticlesNum_NextPow2, 2);
        PTypesNum = pTypeInput.particleTypeStates.Length * 3;

        if (!causticsLoaded)
        {
            CausticsType = Application.isEditor ? CausticsTypeEditor : CausticsTypeBuild;
            if (precomputedCausticsTexture == null && CausticsType == CausticsType.Precomputed)
            {
                Debug.LogWarning("Precomputed caustics texture 2D array not assigned in inspector. Defaulting to CausticsType.None");
                CausticsType = CausticsType.None;
            }
            if (precomputedCausticsTexture != null)
            {
                if (precomputedCausticsTexture.name == "PrecomputedCaustics_400xy_120z" && !PM.hasBeenReset) Debug.LogError("Heavy caustics texture should be loaded as an addressable for lower load times");
                PrecomputedCausticsDims = new(precomputedCausticsTexture.width, precomputedCausticsTexture.height, precomputedCausticsTexture.depth);
            }
        }
    }

    private void InitializeBuffers(PData[] PDatas, RBData[] RBDatas, RBVector[] RBVectors, SensorArea[] SensorAreas)
    {
        ComputeHelper.CreateStructuredBuffer<PData>(ref PDataBuffer, MaxParticlesNum);
        ComputeHelper.CreateStructuredBuffer<PTypeData>(ref PTypeBuffer, pTypeInput.GetParticleTypesData(MatIndexMap));
        ComputeHelper.CreateStructuredBuffer<RecordedFluidData>(ref RecordedFluidDataBuffer, ChunksNumAll);

        ComputeHelper.CreateStructuredBuffer<int2>(ref SpatialLookupBuffer, ParticlesNum_NextPow2);
        ComputeHelper.CreateStructuredBuffer<int>(ref StartIndicesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int2>(ref SpringCapacitiesBuffer, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbA, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbB, ChunksNumAll);
        ComputeHelper.CreateStructuredBuffer<int>(ref SpringStartIndicesBuffer_dbC, ChunksNumAll);

        ComputeHelper.CreateStructuredBuffer<Spring>(ref ParticleSpringsCombinedBuffer, MaxParticlesNum * MaxSpringsPerParticle);

        ComputeHelper.CreateStructuredBuffer<RBData>(ref RBDataBuffer, RBDatas);
        ComputeHelper.CreateStructuredBuffer<RBVector>(ref RBVectorBuffer, RBVectors);
        ComputeHelper.CreateStructuredBuffer<RBAdjustment>(ref RBAdjustmentBuffer, NumRigidBodies);

        ComputeHelper.CreateStructuredBuffer<SensorArea>(ref SensorAreaBuffer, SensorAreas);

        ComputeHelper.CreateStructuredBuffer<Mat>(ref MaterialBuffer, Mats);

        PDataBuffer.SetData(PDatas, 0, 0, ParticlesNum);
    }

    private void GPUSortChunkLookUp()
    {
        int threadGroupsNum = Utils.GetThreadGroupsNums(ParticlesNum_NextPow2, sortShaderThreadSize);
        int threadGroupsNumHalfCeil = (int)Math.Ceiling(threadGroupsNum * 0.5f);

        ComputeHelper.DispatchKernel(sortShader, "CalculateChunkKeys", threadGroupsNum);

        int len = ParticlesNum_NextPow2;
        int sortIterationKernelIndex = sortShader.FindKernel("SortIteration");

        int basebBlockLen = 2;
        if (threadGroupsNumHalfCeil > 0)
            while (basebBlockLen != 2 * len)
            {
                int blockLen = basebBlockLen;
                while (blockLen != 1)
                {
                    bool BrownPinkSort = blockLen == basebBlockLen;

                    sortShader.SetInt("BlockLen_BrownPinkSort", blockLen * (BrownPinkSort ? 1 : -1));
                    sortShader.Dispatch(sortIterationKernelIndex, threadGroupsNumHalfCeil, 1, 1);

                    blockLen /= 2;
                }
                basebBlockLen *= 2;
            }

        ComputeHelper.DispatchKernel(sortShader, "PopulateStartIndices", threadGroupsNum);
    }

    private void GPUSortSpringLookUp()
    {
        if (DoSimulateParticleSprings)
        {
            int threadGroupsNum = Utils.GetThreadGroupsNums(ChunksNumAll, sortShaderThreadSize);

            ComputeHelper.DispatchKernel(sortShader, "PopulateChunkSizes", threadGroupsNum);
            ComputeHelper.DispatchKernel(sortShader, "PopulateSpringCapacities", threadGroupsNum);
            ComputeHelper.DispatchKernel(sortShader, "CopySpringCapacities", threadGroupsNum);

            int ppssKernelIndex = sortShader.FindKernel("ParallelPrefixSumScan");

            bool StepBufferCycle = false;
            if (threadGroupsNum > 0)
                for (int offset = 1; offset < ChunksNumAll; offset *= 2)
                {
                    StepBufferCycle = !StepBufferCycle;

                    sortShader.SetInt("Offset2_StepBufferCycle", offset * (StepBufferCycle ? 1 : -1));
                    sortShader.Dispatch(ppssKernelIndex, threadGroupsNum, 1, 1);
                }

            if (StepBufferCycle == true) ComputeHelper.DispatchKernel(sortShader, "CopySpringStartIndicesBuffer", threadGroupsNum);
        }
    }

    private void RunPSimShader(int step)
    {
        if (ParticlesNum <= 0) return;

        ComputeHelper.DispatchKernel(pSimShader, "PreCalculations", ParticlesNum, pSimShaderThreadSize1);
        ComputeHelper.DispatchKernel(pSimShader, "CalculateDensities", ParticlesNum, pSimShaderThreadSize1);

        if (step == 0 && DoSimulateParticleSprings)
        {
            ComputeHelper.DispatchKernel(pSimShader, "PrepSpringData", ParticleSpringsCombinedHalfLength, pSimShaderThreadSize1);
            ComputeHelper.DispatchKernel(pSimShader, "TransferAllSpringData", ParticleSpringsCombinedHalfLength, pSimShaderThreadSize1);
        }

        ComputeHelper.DispatchKernel(pSimShader, "ParticleForces", ParticlesNum, pSimShaderThreadSize1);

        ComputeHelper.DispatchKernel(pSimShader, "ResetFluidData", ChunksNumAll, pSimShaderThreadSize2);
        ComputeHelper.DispatchKernel(pSimShader, "RecordFluidData", ParticlesNum, pSimShaderThreadSize1);
    }

    private void RunRbSimShader()
    {
        ComputeHelper.DispatchKernel(rbSimShader, "SimulateRB_RB", NumRigidBodies, rbSimShaderThreadSize2);
        ComputeHelper.DispatchKernel(rbSimShader, "SimulateRBSprings", NumRigidBodies, rbSimShaderThreadSize2);
        ComputeHelper.DispatchKernel(rbSimShader, "AdjustRBDatas", NumRigidBodies, rbSimShaderThreadSize2);
        ComputeHelper.DispatchKernel(rbSimShader, "SimulateRB_P", ParticlesNum, rbSimShaderThreadSize3);
        ComputeHelper.DispatchKernel(rbSimShader, "ResetRBVertices", NumRigidBodyVectors, rbSimShaderThreadSize1);
        ComputeHelper.DispatchKernel(rbSimShader, "UpdateRigidBodies", NumRigidBodies, rbSimShaderThreadSize2);
        ComputeHelper.DispatchKernel(rbSimShader, "UpdateRBVertices", NumRigidBodyVectors, rbSimShaderThreadSize1);
    }

    private void DispatchRenderStep(RenderStep step, int2 threadsNum)
    {
        switch (step)
        {
            case RenderStep.Background:
                ComputeHelper.DispatchKernel(renderShader, "RenderBackground", threadsNum, renderShaderThreadSize);
                break;
            case RenderStep.Fluids:
                if (ParticlesNum > 0) ComputeHelper.DispatchKernel(renderShader, "RenderFluids", threadsNum, renderShaderThreadSize);
                break;
            case RenderStep.RigidBodies:
                if (NumRigidBodies > 0) ComputeHelper.DispatchKernel(renderShader, "RenderRigidBodies", threadsNum, renderShaderThreadSize);
                break;
            case RenderStep.RigidBodySprings:
                if (NumRigidBodies > 0) ComputeHelper.DispatchKernel(renderShader, "RenderRigidBodySprings", threadsNum, renderShaderThreadSize);
                break;
            case RenderStep.UI:
                ComputeHelper.DispatchKernel(renderShader, "RenderUI", threadsNum, renderShaderThreadSize);
                break;
        }
    }

    public void RunRenderShader()
    {
        if (PM.Instance != null && PM.Instance.sceneIsResetting) return;
        if (renderTexture == null || ppRenderTexture == null) return;

        PM.Instance.timeSetRandTimer += PM.Instance.clampedDeltaTime;
        if (TimeSetRandInterval == 0) TimeSetRandInterval = 0.01f;
        if (PM.Instance.timeSetRandTimer > TimeSetRandInterval)
        {
            renderShader.SetInt("LastTimeSetRand", timeSetRand);
            timeSetRand = Func.RandInt(0, 99999);
            renderShader.SetInt("NextTimeSetRand", timeSetRand);
            PM.Instance.timeSetRandTimer %= TimeSetRandInterval;
        }
        renderShader.SetFloat("TimeSetLerpFactor", PM.Instance.timeSetRandTimer / TimeSetRandInterval);
        renderShader.SetInt("PrecomputedCausticsZ", Mathf.FloorToInt(PM.Instance.totalScaledTimeElapsed * PrecomputedCausticsFPS));

        ppShader.SetFloat("GlobalBrightnessFactor", PM.Instance.globalBrightnessFactor);

        int2 threadsNum = new(renderTexture.width, renderTexture.height);
        foreach (RenderStep step in RenderOrder)
        {
            if (step == RenderStep.Fluids && ParticlesNum == 0) continue;
            DispatchRenderStep(step, threadsNum);
        }

        RunPPShader();
    }

    public void RunPPShader()
    {
        if (AllocateOrResizeShadowWorkingBuffers())
        {
            shaderHelper.SetRenderShaderBuffers(renderShader);
            shaderHelper.SetPostProcessorBuffers(ppShader);
            shaderHelper.SetPostProcessorVariables(ppShader);
        }

        int2 fullThreads2D = new(renderTexture.width, renderTexture.height);
        int2 shadowThreads2D = GetShadowResolution();
        int shadowThreadsX = shadowThreads2D.x;

        void Shadows()
        {
            void GenerateShadows()
            {
                int kDown = ppShader.FindKernel("DownsampleShadowMask");
                if (kDown >= 0)
                {
                    ComputeHelper.DispatchKernel(ppShader, "DownsampleShadowMask", shadowThreads2D, ppShaderThreadSize2);
                }

                if (ShadowType == ShadowType.Vertical_Sharp || ShadowType == ShadowType.Vertical_Blurred)
                {
                    ComputeHelper.DispatchKernel(ppShader, "CreateShadowsVertical", shadowThreadsX, ppShaderThreadSize1);
                }
                else if (ShadowType == ShadowType.Diagonal_Sharp || ShadowType == ShadowType.Diagonal_Blurred)
                {
                    ComputeHelper.DispatchKernel(ppShader, "CreateShadowsDiagonal", shadowThreadsX, ppShaderThreadSize1);
                }
                else if (ShadowType == ShadowType.Directional_Sharp || ShadowType == ShadowType.Directional_Blurred)
                {
                    ComputeHelper.DispatchKernel(ppShader, "CreateShadowsDirectional", shadowThreadsX, ppShaderThreadSize1);
                }
            }

            void BlurAndApplyShadows()
            {
                bool isBlurred =
                    ShadowType == ShadowType.Vertical_Blurred ||
                    ShadowType == ShadowType.Diagonal_Blurred ||
                    ShadowType == ShadowType.Directional_Blurred;

                if (isBlurred)
                {
                    ComputeHelper.DispatchKernel(ppShader, "CopySharpShadows", shadowThreads2D, ppShaderThreadSize2);

                    bool stepBufferCycle = true;
                    for (int i = 0; i < ShadowBlurIterations; i++)
                    {
                        ppShader.SetBool("StepBufferCycle", stepBufferCycle);
                        ppShader.SetInt("BlurOffset", Func.Pow2(ShadowBlurIterations - 1 - i));
                        ComputeHelper.DispatchKernel(ppShader, "BlurShadowsGaussian", shadowThreads2D, ppShaderThreadSize2);
                        stepBufferCycle = !stepBufferCycle;
                    }

                    ppShader.SetBool("StepBufferCycle", !stepBufferCycle);
                    ComputeHelper.DispatchKernel(ppShader, "ApplyBlurredShadows", fullThreads2D, ppShaderThreadSize2);
                }
                else
                {
                    ComputeHelper.DispatchKernel(ppShader, "ApplySharpShadows", fullThreads2D, ppShaderThreadSize2);
                }
            }

            GenerateShadows();
            BlurAndApplyShadows();
        }

        void ApplyAA()
        {
            if (DoUseAntiAliasing)
            {
                ComputeHelper.DispatchKernel(ppShader, "ApplyAA", fullThreads2D, ppShaderThreadSize2);
                ComputeHelper.DispatchKernel(ppShader, "CopyResultToPP", fullThreads2D, ppShaderThreadSize2);
            }
        }

        if (ShadowType == ShadowType.None)
        {
            ComputeHelper.DispatchKernel(ppShader, "ApplyWithoutShadows", fullThreads2D, ppShaderThreadSize2);
            ApplyAA();
            return;
        }

        Shadows();
        ApplyAA();

        ppFrameValid = true;
    }

    private void InitTimeSetRand()
    {
        renderShader.SetInt("LastTimeSetRand", Func.RandInt(0, 99999));
        timeSetRand = Func.RandInt(0, 99999);
        renderShader.SetInt("NextTimeSetRand", timeSetRand);
    }

    private void TryLoadAddressableCaustics()
    {
        var addressableCausticsTexture = shaderHelper.addressableCausticsTexture;
        if (addressableCausticsTexture == null) return;
        if (!addressableCausticsTexture.RuntimeKeyIsValid()) return;

        // If a previous coroutine is running, stop it (we'll start a fresh one)
        if (_causticsCoroutine != null)
        {
            StopCoroutine(_causticsCoroutine);
            _causticsCoroutine = null;
        }
        _causticsCoroutine = StartCoroutine(LoadAddressableCaustics_Coroutine());
    }

    private IEnumerator LoadAddressableCaustics_Coroutine()
    {
        int myVersion = ++_causticsLoadVersion; // reserve a new version for this run

        // first-start stagger (avoid fighting with other heavy startup tasks)
        if (!PM.hasBeenReset) yield return new WaitForSecondsRealtime(2f);

        // If we already succeeded and nothing was invalidated, just exit
        if (causticsLoaded && precomputedCausticsTexture != null)
        {
            yield break;
        }

        // Ensure any previous reference/handle is released BEFORE calling LoadAssetAsync again
        try
        {
            var aref = shaderHelper.addressableCausticsTexture;
            if (aref != null && aref.IsValid())
            {
                aref.ReleaseAsset();
            }
            else if (_causticsHandle.IsValid())
            {
                Addressables.Release(_causticsHandle);
            }
        }
        catch { /* ignore double release */ }

        // Safe re-init
        var init = Addressables.InitializeAsync();
        yield return init;

        if (myVersion != _causticsLoadVersion) yield break; // cancelled by a reset

        // Now load
        _causticsHandle = shaderHelper.addressableCausticsTexture.LoadAssetAsync<Texture2DArray>();
        yield return _causticsHandle;

        if (myVersion != _causticsLoadVersion) yield break; // cancelled by a reset

        if (_causticsHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var newTex = _causticsHandle.Result;

            precomputedCausticsTexture = newTex;
            causticsLoaded = true;

            if (CausticsType == CausticsType.Precomputed)
            {
                ApplyPrecomputedCausticsToShader(newTex);
            }
        }
        else
        {
            causticsLoaded = false;
            Debug.LogError(_causticsHandle.OperationException ?? new System.Exception("Failed to load Addressable caustics texture."));
        }
    }

    private void ApplyPrecomputedCausticsToShader(Texture2DArray tex)
    {
        if (tex == null) { Debug.LogError("ApplyPrecomputedCausticsToShader texture is null."); return; }
        PrecomputedCausticsDims = new(tex.width, tex.height, tex.depth);
        renderShader.SetVector("PrecomputedCausticsDims", Utils.Int3ToVector3(PrecomputedCausticsDims));
        renderShader.SetTexture(1, "PrecomputedCaustics", tex);

        ppFrameValid = false;
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // During reset just pass through the camera so we don't flash black.
        if (PM.Instance != null && PM.Instance.sceneIsResetting)
        {
            Graphics.Blit(src, dest);
            return;
        }

        if (renderTexture == null)
        {
            Graphics.Blit(src, dest);
            return;
        }
        
        RenderTexture finalRT = DoUseAntiAliasing ? ppRenderTexture : renderTexture;

        if (finalRT == null)
        {
            // Texture not ready yet
            Graphics.Blit(src, dest);
            return;
        }

        // Ensure we've produced at least one frame before blitting our RTs.
        // (This can happen at paused start or right after soft/hard reset.)
        if (!ppFrameValid)
        {
            RunGPUSorting();
            RunRenderShader();
        }

        // Final blit.
        Graphics.Blit(finalRT, dest);
    }

    public void SoftReset()
    {
        ReleaseResources();
        StartScript();
    }

    public void ReleaseResources()
    {
        // Stop any in-flight caustics loading and invalidate it
        if (_causticsCoroutine != null)
        {
            try { StopCoroutine(_causticsCoroutine); } catch { }
            _causticsCoroutine = null;
        }
        _causticsLoadVersion++; // invalidate any pending yield continuations

        ComputeHelper.Release(
            SpatialLookupBuffer,
            StartIndicesBuffer,
            PDataBuffer,
            PTypeBuffer,
            RecordedFluidDataBuffer,
            SpringCapacitiesBuffer,
            SpringStartIndicesBuffer_dbA,
            SpringStartIndicesBuffer_dbB,
            SpringStartIndicesBuffer_dbC,
            ParticleSpringsCombinedBuffer,
            RBDataBuffer,
            RBAdjustmentBuffer,
            SensorAreaBuffer,
            RBVectorBuffer,
            MaterialBuffer,
            ShadowSrcFullRes,
            ShadowMask_dbA,
            ShadowMask_dbB,
            SharpShadowMask,
            ShadowDstMask,
            RimLightMask
        );

        // Release render textures if they exist
        if (renderTexture != null) { renderTexture.Release(); Destroy(renderTexture); renderTexture = null; }
        if (ppRenderTexture != null) { ppRenderTexture.Release(); Destroy(ppRenderTexture); ppRenderTexture = null; }

        AtlasTexture = null;
        LiquidVelocityGradientTexture = null;
        GasVelocityGradientTexture = null;

        // Proper Addressables release (prefer releasing via AssetReference if valid; otherwise by handle)
        try
        {
            var aref = shaderHelper != null ? shaderHelper.addressableCausticsTexture : null;
            if (aref != null && aref.IsValid())
            {
                aref.ReleaseAsset();
            }
            else if (_causticsHandle.IsValid())
            {
                Addressables.Release(_causticsHandle);
            }
        }
        catch { /* swallow double release */ }

        _causticsHandle = default;
        causticsLoaded = false;

        // Reset runtime counters/lightweight state
        NewPDatas.Clear();
        gpuDataSorted = false;
        StepCount = 0;
        SimTimeElapsed = 0f;
        ParticlesNum = 0;
        NumRigidBodies = 0;
        NumRigidBodyVectors = 0;
        NumFluidSensors = 0;
        _cachedShadowRes = int2.zero;
        ppFrameValid = false;
    }

    private void OnDestroy()
    {
        ReleaseResources();
    }

    private int2 GetShadowResolution()
    {
        int factor = 1 << Mathf.Clamp(ShadowDownSampling, 0, 30);
        int w = Mathf.Max(1, renderTexture != null ? renderTexture.width / factor : PM.Instance.ResolutionInt2.x / factor);
        int h = Mathf.Max(1, renderTexture != null ? renderTexture.height / factor : PM.Instance.ResolutionInt2.y / factor);
        return new int2(w, h);
    }

    private bool AllocateOrResizeShadowWorkingBuffers()
    {
        int2 desired = GetShadowResolution();
        if (_cachedShadowRes.x == desired.x && _cachedShadowRes.y == desired.y &&
            ShadowMask_dbA != null && ShadowMask_dbB != null && SharpShadowMask != null &&
            ShadowDstMask != null && RimLightMask != null)
        {
            return false;
        }

        ComputeHelper.Release(ShadowMask_dbA, ShadowMask_dbB, SharpShadowMask, ShadowDstMask, RimLightMask);

        int count = Mathf.Max(1, desired.x * desired.y);
        ComputeHelper.CreateStructuredBuffer<float>(ref ShadowMask_dbA, count);
        ComputeHelper.CreateStructuredBuffer<float>(ref ShadowMask_dbB, count);
        ComputeHelper.CreateStructuredBuffer<float>(ref SharpShadowMask, count);
        ComputeHelper.CreateStructuredBuffer<float>(ref ShadowDstMask, count);
        ComputeHelper.CreateStructuredBuffer<float>(ref RimLightMask, count);

        _cachedShadowRes = desired;
        return true;
    }

    private void RecreateOrUpdateMaterialBuffer()
    {
        int count = (Mats != null) ? Mats.Length : 0;

        // If no materials, release any existing buffer and bail
        if (count <= 0)
        {
            ComputeHelper.Release(MaterialBuffer);
            MaterialBuffer = null;
            return;
        }

        int requiredStride = Marshal.SizeOf<Mat>();

        // If existing buffer is invalid or wrong shape/stride, recreate it
        bool needsRecreate = false;
        if (MaterialBuffer == null)
        {
            needsRecreate = true;
        }
        else
        {
            try
            {
                if (MaterialBuffer.count != count || MaterialBuffer.stride != requiredStride)
                    needsRecreate = true;
            }
            catch
            {
                needsRecreate = true;
            }
        }

        if (needsRecreate)
        {
            ComputeHelper.Release(MaterialBuffer);
            ComputeHelper.CreateStructuredBuffer<Mat>(ref MaterialBuffer, Mats);
        }
        else
        {
            MaterialBuffer.SetData(Mats);
        }
    }

    private void BuildAtlasAndMats()
    {
        var uniques = new List<CustomMat>();
        var seen = new HashSet<CustomMat>();
        void AddIf(CustomMat cm) { if (cm == null) return; if (seen.Add(cm)) uniques.Add(cm); }

        foreach (var rb in SceneManager.GetAllSceneRigidBodies())
        {
            AddIf(rb.material);
            AddIf(rb.springMaterial);
        }

        if (pTypeInput != null && pTypeInput.particleTypeStates != null)
        {
            foreach (var pts in pTypeInput.particleTypeStates)
            {
                AddIf(pts.solidState.material);
                AddIf(pts.liquidState.material);
                AddIf(pts.gasState.material);
            }
        }

        AddIf(MetalHighlightsCustomMat);
        AddIf(BackgroundCustomMat);

        var ordered = new List<CustomMat>(_stableMatOrder.Count + uniques.Count);
        var known = new HashSet<CustomMat>(_stableMatOrder);

        foreach (var cm in _stableMatOrder)
            if (uniques.Contains(cm))
                ordered.Add(cm);

        uniques.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));
        foreach (var cm in uniques)
            if (!known.Contains(cm))
                ordered.Add(cm);

        _stableMatOrder.Clear();
        _stableMatOrder.AddRange(ordered);

        (AtlasTexture, Mats) = sceneManager.ConstructTextureAtlas(ordered.ToArray());

        MatIndexMap.Clear();
        for (int i = 0; i < ordered.Count; i++)
            MatIndexMap[ordered[i]] = i;

        BackgroundMatIndex = (BackgroundCustomMat != null && MatIndexMap.TryGetValue(BackgroundCustomMat, out var bi)) ? bi : -1;
        MetalHighlightsMatIndex = (MetalHighlightsCustomMat != null && MatIndexMap.TryGetValue(MetalHighlightsCustomMat, out var mi)) ? mi : -1;

        RecreateOrUpdateMaterialBuffer();

        if (PM.Instance.programStarted && renderTexture != null)
        {
            shaderHelper.SetRenderShaderTextures(renderShader);
            shaderHelper.SetRenderShaderBuffers(renderShader);
            shaderHelper.UpdateRenderShaderVariables(renderShader);
        }
    }

    public void OnBackgroundMatChanged()
    {
        BuildAtlasAndMats();
        UpdateSettings();
    }

    public void OnMetalHighlightsMatChanged()
    {
        BuildAtlasAndMats();
        UpdateSettings();
    }
}