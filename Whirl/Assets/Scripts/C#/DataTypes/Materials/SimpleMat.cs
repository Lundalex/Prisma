using UnityEngine;

[CreateAssetMenu(menuName = "CustomMats/SimpleMat", fileName = "SimpleMat")]
public class SimpleMat : CustomMat
{
    public Texture2D colTexture;
}






















The TextureBaker should just call a function in main to let it know to upadte it's background. Additioanlly, add a field for a BaseMat in Main for the background, and have it replace the earlier texture, upscaling, and mirror-repeat fields. Keep the brightness field. Give me all the required changes, but DONT GIVE ME THE FULL CODE, JUST THE CHANGES. using UnityEngine;
using Unity.Mathematics;
using System;
using System.Collections;
using System.Collections.Generic;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Resources2;
using PM = ProgramManager;
using Debug = UnityEngine.Debug;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

public class Main : MonoBehaviour
{
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
    // Fluids
    public bool DoSimulateParticleViscosity = true;
    public bool DoSimulateParticleSprings = true;
    public bool DoSimulateParticleTemperature = true;

    // Variable storage precision
    public float FloatIntPrecisionRB = 50000.0f; // Rigid Body Simulation
    public float FloatIntPrecisionRBRot = 500000.0f; // Rigid Body Simulation
    public float FloatIntPrecisionP = 1000.0f; // Particle Simulation
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
    // Particles
    public float MaxInteractionRadius = 40.0f;
    public float InteractionAttractionPower = 3.5f;
    public float InteractionRepulsionPower = 3.5f;
    public float InteractionFountainPower = 1.0f;
    public float InteractionTemperaturePower = 1.0f;
    public float InteractionDampening = 1.0f;

    // Rigid Bodies
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

    // The list that defines the order of render steps
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

    public float RimShadingStrength = 2.0f;
    public float RimShadingBleed = 0.2f;
    public float RimShadingOpaqueBleed = 3.0f;
    public int ShadowDownSampling = 1;
    #endregion

    #region Render Display
    public int2 Resolution;
    public Vector2 UIPadding;

    // === New: Sun light + RB world UV control ===
    [Header("Lighting / RB Mapping")]
    public Vector2 SunDirection = new(-0.71f, -0.71f); // XY on screen
    [Tooltip("World/simulation units per 1 albedo UV tile for rigid bodies.")]

    public LightingSettings LightingSettings;
    public float3 GlobalBrightness;
    public float Contrast;
    public float Saturation;
    public float Gamma;
    public float SettingsViewDarkTintPercent;
    public float PrecomputedCausticsFPS;
    public float PrecomputedCausticsScaleFactor;
    public float DynamicCausticsScaleFactor;
    public float PrecomputedCausticsZBlurFactor;

    // Rigid Body Springs
    public float SpringRenderWidth;
    public float SpringRenderMatWidth;
    public float SpringRenderRodLength;
    public int SpringRenderNumPeriods;
    public float TaperThresoldNormalised = 0.2f;
    public float2 SpringTextureUVFactor = new(10.0f, 1.0f);

    // Fluids
    // Liquids
    public float LiquidMetaballsThreshold = 1.0f;
    public float LiquidMetaballsEdgeDensityWidth = 0.3f;
    public float VisualLiquidParticleRadius = 0.4f;
    public float LiquidEdgeWidth = 1.0f;

    // Liquid Velocity Gradient
    public Gradient LiquidVelocityGradient;
    public int LiquidVelocityGradientResolution;
    public float LiquidVelocityGradientMaxValue;

    // Gasses
    public float GasMetaballsThreshold = 1.0f;
    public float GasMetaballsEdgeDensityWidth = 0.3f;
    public float VisualGasParticleRadius = 0.4f;
    public float GasEdgeWidth = 1.0f;
    public float GasNoiseStrength = 1.0f;
    public float GasNoiseDensityDarkeningFactor;
    public float GasNoiseDensityOpacityFactor;
    public float TimeSetRandInterval = 0.5f;

    // Gas Velocity Gradient
    public Gradient GasVelocityGradient;
    public int GasVelocityGradientResolution;
    public float GasVelocityGradientMaxValue;

    // Rigid Bodies
    public float RBEdgeWidth = 0.5f;

    // Sensor Areas
    public float FluidSensorEdgeWidth = 3.0f;
    public float SensorAreaAnimationSpeed = 2.0f;

    // Background
    public float GlobalSettingsViewChangeSpeed;
    public Texture2D backgroundTexture;
    public float3 BackgroundBrightness;
    public float BackgroundUpScaleFactor;
    public bool MirrorRepeatBackgroundUV;

    // Rigid body path flags
    public static readonly float PathFlagOffset = 100000.0f;
    public static readonly float PathFlagThreshold = PathFlagOffset / 2.0f;
    #endregion

    #region References
    // Textures
    public RenderTexture uiTexture;
    public RenderTexture dynamicCausticsTexture;
    public Texture2DArray precomputedCausticsTexture;

    public MaterialInput materialInput;
    public PTypeInput pTypeInput;
    public SceneManager sceneManager;
    public ShaderHelper shaderHelper;
    public Transform fragmentTransform;

    // Compute Shaders
    public ComputeShader renderShader;
    public ComputeShader ppShader;
    public ComputeShader pSimShader;
    public ComputeShader rbSimShader;
    public ComputeShader sortShader;
    public ComputeShader debugShader;
    #endregion

    // Bitonic mergesort
    public ComputeBuffer SpatialLookupBuffer;
    public ComputeBuffer StartIndicesBuffer;

    // Inter-particle springs
    public ComputeBuffer SpringCapacitiesBuffer;
    private bool FrameBufferCycle = true;
    public ComputeBuffer SpringStartIndicesBuffer_dbA; // Result A
    public ComputeBuffer SpringStartIndicesBuffer_dbB; // Result B
    public ComputeBuffer SpringStartIndicesBuffer_dbC; // Support
    public ComputeBuffer ParticleSpringsCombinedBuffer; // [[Last frame springs], [New frame springs]]

    // Particle data
    public ComputeBuffer PDataBuffer;
    public ComputeBuffer PTypeBuffer;
    public ComputeBuffer RecordedFluidDataBuffer;

    // Rigid bodies
    public ComputeBuffer RBVectorBuffer;
    public ComputeBuffer RBDataBuffer;
    public ComputeBuffer RBAdjustmentBuffer;

    // Fluid Sensors
    public ComputeBuffer SensorAreaBuffer;
    // Materials
    public ComputeBuffer MaterialBuffer;

    // Shadows
    // Full-resolution source mask written by RenderShader; downsampled before shadow casting
    public ComputeBuffer ShadowSrcFullRes;
    // Low-resolution shadow working buffers (size = ShadowResolution = Resolution / 2^ShadowDownSampling)
    public ComputeBuffer ShadowMask_dbA;
    public ComputeBuffer ShadowMask_dbB;
    public ComputeBuffer SharpShadowMask;
    public ComputeBuffer ShadowDstMask;
    public ComputeBuffer RimLightMask;

    // Constants
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

    // Shader Thread Group Sizes
    public const int renderShaderThreadSize = 16; // /32, AxA thread groups
    public const int ppShaderThreadSize1 = 32; // /1024
    public const int ppShaderThreadSize2 = 16; // /32
    public const int pSimShaderThreadSize1 = 512; // /1024
    public const int pSimShaderThreadSize2 = 512; // /1024
    public const int sortShaderThreadSize = 512; // /1024
    public const int rbSimShaderThreadSize1 = 64; // Rigid Body Simulation
    public const int rbSimShaderThreadSize2 = 32; // Rigid Body Simulation
    public const int rbSimShaderThreadSize3 = 512; // Rigid Body Simulation

    // Private references
    [NonSerialized] public RenderTexture renderTexture;
    [NonSerialized] public RenderTexture ppRenderTexture;
    [NonSerialized] public Texture2D AtlasTexture;
    [NonSerialized] public Texture2D LiquidVelocityGradientTexture;
    [NonSerialized] public Texture2D GasVelocityGradientTexture;
    [NonSerialized] public GameObject causticsGen;

    // New particles data
    private List<PData> NewPDatas = new();

    // Materials
    private Mat[] Mats;

    // Other
    private float DeltaTime;
    private float RLDeltaTime;
    private float SimTimeElapsed;
    private int StepCount = 0;
    private int timeSetRand;
    private bool gpuDataSorted = false;
    [NonSerialized] public static bool2 MousePressed = false; // (left, right)

    // Addressables (caustics) state
    private AsyncOperationHandle<Texture2DArray> _causticsHandle;
    private bool _causticsLoadStarted;

    // Cache for current shadow working resolution (to handle runtime ShadowDownSampling changes)
    private int2 _cachedShadowRes = int2.zero;

    // Expose current material count to helper
    public int MaterialsCount => Mats != null ? Mats.Length : 0;

    public void SubmitParticlesToSimulation(PData[] particlesToAdd) => NewPDatas.AddRange(particlesToAdd);

    public void StartScript()
    {
        SimTimeElapsed = 0;

        ValidateHardwareCompatibility();
        RenderSetup();

        // Boundary
        BoundaryDims = sceneManager.GetBounds(MaxInfluenceRadius);
        ChunksNum = BoundaryDims / MaxInfluenceRadius;
        ChunksNumAll = ChunksNum.x * ChunksNum.y;

        // Particles
        PData[] PDatas = sceneManager.GenerateParticles(MaxStartingParticlesNum);
        ParticlesNum = PDatas.Length;

        // Rigid bodies & sensor areas
        (RBData[] RBDatas, RBVector[] RBVectors, SensorArea[] SensorAreas) = sceneManager.CreateRigidBodies();
        NumRigidBodies = RBDatas.Length;
        NumRigidBodyVectors = RBVectors.Length;
        NumFluidSensors = SensorAreas.Length;

        // Materials / atlas / gradients
        (AtlasTexture, Mats) = sceneManager.ConstructTextureAtlas(materialInput.materialInputs);
        TextureHelper.TextureFromGradient(ref LiquidVelocityGradientTexture, LiquidVelocityGradientResolution, LiquidVelocityGradient);
        TextureHelper.TextureFromGradient(ref GasVelocityGradientTexture, GasVelocityGradientResolution, GasVelocityGradient);

        SetConstants();
        InitTimeSetRand();
        SetLightingSettings();

        // Initialize buffers
        InitializeBuffers(PDatas, RBDatas, RBVectors, SensorAreas);
        renderTexture = TextureHelper.CreateTexture(PM.Instance.ResolutionInt2, 3);
        ppRenderTexture = TextureHelper.CreateTexture(PM.Instance.ResolutionInt2, 3);

        // --- Shadow buffers (full + downsampled) ---
        // Full-res source written by renderShader
        ComputeHelper.CreateStructuredBuffer<float>(ref ShadowSrcFullRes, renderTexture.width * renderTexture.height);

        // Downsampled working buffers (size depends on ShadowDownSampling, and can change at runtime)
        AllocateOrResizeShadowWorkingBuffers();

        // Shader buffers/textures
        shaderHelper.SetPSimShaderBuffers(pSimShader);
        shaderHelper.SetRBSimShaderBuffers(rbSimShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);      // will bind ShadowSrcFullRes to renderShader
        shaderHelper.SetRenderShaderTextures(renderShader);
        shaderHelper.SetPostProcessorBuffers(ppShader);         // will bind ShadowSrcFullRes + low-res working buffers to ppShader
        shaderHelper.SetPostProcessorTextures(ppShader);
        shaderHelper.SetSortShaderBuffers(sortShader);

        // Shader variables
        shaderHelper.UpdatePSimShaderVariables(pSimShader);
        shaderHelper.UpdateRBSimShaderVariables(rbSimShader);
        shaderHelper.UpdateRenderShaderVariables(renderShader);
        shaderHelper.SetPostProcessorVariables(ppShader);       // will also set ShadowResolution for PP
        shaderHelper.UpdateSortShaderVariables(sortShader);

        SetShaderKeywords();
        InitCausticsGen();

#if UNITY_EDITOR
        ComputeShaderDebugger.CheckShaderConstants(this, debugShader, pTypeInput);
#endif

        // Initialize the shader pipeline
        UpdateShaderTimeStep();
        GPUSortChunkLookUp();
        GPUSortSpringLookUp();
        PM.Instance.clampedDeltaTime = Const.SMALL_FLOAT;
        UpdateScript();

        StringUtils.LogIfInEditor("Simulation started with " + ParticlesNum + " particles, " + NumRigidBodies + " rigid bodies, and " + NumRigidBodyVectors + " vertices. Platform: " + Application.platform);

        // If an addressable caustics texture is assigned, load it and set precomputedCausticsTexture.
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

                // Transfer the new particle data to the GPU
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

        // === Rebuild materials/atlas and gradients when inspector inputs change ===
        (AtlasTexture, Mats) = sceneManager.ConstructTextureAtlas(materialInput.materialInputs);
        TextureHelper.TextureFromGradient(ref LiquidVelocityGradientTexture, LiquidVelocityGradientResolution, LiquidVelocityGradient);
        TextureHelper.TextureFromGradient(ref GasVelocityGradientTexture, GasVelocityGradientResolution, GasVelocityGradient);

        // Ensure Material buffer fits current material count
        RecreateOrUpdateMaterialBuffer();

        // Rebind changed textures/buffers
        shaderHelper.SetRenderShaderTextures(renderShader);
        shaderHelper.SetRenderShaderBuffers(renderShader);

        // Push all uniforms again (includes sunDir & RBWorldUVScale)
        UpdateSettings();

        SetShaderKeywords();
        InitCausticsGen();

        // Handle runtime changes to shadow working resolution (e.g., ShadowDownSampling modified by user)
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
        // Set new pType and material data
        PTypeBuffer.SetData(pTypeInput.GetParticleTypes());

        // Material buffer may have been resized in UpdateShaderData; if not, just SetData.
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
        // Mouse position
        Vector2 mouseSimPos = GetMousePosInSimSpace(false);

        // Mouse button input handling
        bool2 currentMouseInputs = Utils.GetMousePressed();
        bool skipUpdatingMouseInputs = (currentMouseInputs.x && MousePressed.x) || (currentMouseInputs.y && MousePressed.y);
        if (!skipUpdatingMouseInputs)
        {
            bool disallowMouseInputs = PM.Instance.CheckAnyUIElementHovered() || PM.Instance.CheckAnySensorBeingMoved() || PM.Instance.isAnySensorSettingsViewActive;
            MousePressed = disallowMouseInputs ? false : currentMouseInputs;
        }

        // Per-timestep-set variables - pSimShader
        pSimShader.SetFloat("DeltaTime", DeltaTime);
        pSimShader.SetFloat("RLDeltaTime", RLDeltaTime);
        pSimShader.SetVector("MousePos", mouseSimPos);
        pSimShader.SetBool("LMousePressed", MousePressed.x);
        pSimShader.SetBool("RMousePressed", MousePressed.y);

        // Per-timestep-set variables - rbSimShader
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
#else
            // Debug.Log("Fast shader compilation enabled in build version. This may slightly decrease runtime performance");
#endif
        }

        // Render shader
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
        else renderShader.DisableKeyword("DRAW_RB_OUTLINE");
        if (FluidRenderMethod == FluidRenderMethod.Metaballs) renderShader.EnableKeyword("USE_METABALLS");
        else renderShader.DisableKeyword("USE_METABALLS");
        if (SampleMethod == SampleMethod.Bilinear) renderShader.EnableKeyword("USE_BILINEAR_SAMPLER");
        else renderShader.DisableKeyword("USE_BILINEAR_SAMPLER");
        if (DoUseFastShaderCompilation) renderShader.EnableKeyword("DO_USE_FAST_COMPILATION");
        else renderShader.DisableKeyword("DO_USE_FAST_COMPILATION");

        // Particle simulation shader
        if (DoSimulateParticleViscosity) pSimShader.EnableKeyword("SIMULATE_PARTICLE_VISCOSITY");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_VISCOSITY");
        if (DoSimulateParticleSprings) pSimShader.EnableKeyword("SIMULATE_PARTICLE_SPRINGS");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_SPRINGS");
        if (DoSimulateParticleTemperature) pSimShader.EnableKeyword("SIMULATE_PARTICLE_TEMPERATURE");
        else pSimShader.DisableKeyword("SIMULATE_PARTICLE_TEMPERATURE");

        // Rigid body simulation shader
        if (DoUseFastShaderCompilation) rbSimShader.EnableKeyword("DO_USE_FAST_COMPILATION");
        else rbSimShader.DisableKeyword("DO_USE_FAST_COMPILATION");
    }

    private void ValidateHardwareCompatibility()
    {
        // This function will determine whether the current simulation device settings are compatible with the user's computer.
        // Otherwise, the simulation device will be changed to prevent crashes.
    }

    private void RenderSetup()
    {
        Camera.main.transform.position = new Vector3(BoundaryDims.x / 2, BoundaryDims.y / 2, -1);
        Camera.main.orthographicSize = Mathf.Max(BoundaryDims.x * 0.75f, BoundaryDims.y * 1.5f);

        if (fragmentTransform != null)
        {
            fragmentTransform.position = new Vector3(BoundaryDims.x / 2, BoundaryDims.y / 2, -0.5f);
            fragmentTransform.localScale = 0.5f * Func.Int2ToVector2(Resolution);
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
        SettingsViewDarkTintPercent = 1.0f;

        // Old code
        // switch (Application.platform)
        // {
        //     case RuntimePlatform.WindowsEditor:
        //         GlobalBrightness = 1.0f;
        //         Contrast = 1.1f;
        //         Saturation = 1.2f;
        //         Gamma = 0.52f;
        //         SettingsViewDarkTintPercent = 0.8f;
        //         break;
        //     case RuntimePlatform.OSXEditor:
        //         GlobalBrightness = new float3(0.8f, 0.8f, 0.8f);
        //         Contrast = 1.2f;
        //         Saturation = 1.1f;
        //         Gamma = 0.7f;
        //         SettingsViewDarkTintPercent = 0.8f;
        //         break;
        //     case RuntimePlatform.WebGLPlayer:
        //         GlobalBrightness = new float3(0.8f, 0.8f, 0.8f);
        //         Contrast = 1.2f;
        //         Saturation = 1.1f;
        //         Gamma = 0.65f;
        //         SettingsViewDarkTintPercent = 0.8f;
        //         break;
        //     default:
        //         Debug.LogError("RuntimePlatform not recognised. Will default to using custom preset");
        //         break;
        // }
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
        else // TimeStepType == TimeStepType.Dynamic
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

        CausticsType = Application.isEditor ? CausticsTypeEditor : CausticsTypeBuild;
        if (precomputedCausticsTexture == null && CausticsType == CausticsType.Precomputed)
        {
            Debug.LogWarning("Precomputed caustics texture 2D array not assigned in inspector. Defaulting to CausticsType.None");
            CausticsType = CausticsType.None;
        }
        if (precomputedCausticsTexture != null)
        {
            if (precomputedCausticsTexture.name == "PrecomputedCaustics_400xy_120z") Debug.LogError("Heavy caustics texture should be loaded as an addressable for lower load times");
            PrecomputedCausticsDims = new(precomputedCausticsTexture.width, precomputedCausticsTexture.height, precomputedCausticsTexture.depth);
        }
    }

    private void InitializeBuffers(PData[] PDatas, RBData[] RBDatas, RBVector[] RBVectors, SensorArea[] SensorAreas)
    {
        ComputeHelper.CreateStructuredBuffer<PData>(ref PDataBuffer, MaxParticlesNum);
        ComputeHelper.CreateStructuredBuffer<PType>(ref PTypeBuffer, pTypeInput.GetParticleTypes());
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
            while (basebBlockLen != 2 * len) // basebBlockLen == len is the last outer iteration
            {
                int blockLen = basebBlockLen;
                while (blockLen != 1) // blockLen == 2 is the last inner iteration
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
            // Spring buffer kernels
            int threadGroupsNum = Utils.GetThreadGroupsNums(ChunksNumAll, sortShaderThreadSize);

            ComputeHelper.DispatchKernel(sortShader, "PopulateChunkSizes", threadGroupsNum);
            ComputeHelper.DispatchKernel(sortShader, "PopulateSpringCapacities", threadGroupsNum);
            ComputeHelper.DispatchKernel(sortShader, "CopySpringCapacities", threadGroupsNum);

            int ppssKernelIndex = sortShader.FindKernel("ParallelPrefixSumScan");

            // Calculate prefix sums (SpringStartIndices)
            bool StepBufferCycle = false;
            if (threadGroupsNum > 0)
                for (int offset = 1; offset < ChunksNumAll; offset *= 2)
                {
                    StepBufferCycle = !StepBufferCycle;

                    sortShader.SetInt("Offset2_StepBufferCycle", offset * (StepBufferCycle ? 1 : -1));

                    sortShader.Dispatch(ppssKernelIndex, threadGroupsNum, 1, 1);
                }

            if (StepBufferCycle == true) ComputeHelper.DispatchKernel(sortShader, "CopySpringStartIndicesBuffer", threadGroupsNum); // copy to result buffer
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
        // TimeSetRand
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

        // Global brightness
        renderShader.SetFloat("GlobalBrightnessFactor", PM.Instance.globalBrightnessFactor);

        // Dispatch render steps
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
        // Make sure shadow buffers match current downsampling (in case user changed ShadowDownSampling at runtime)
        if (AllocateOrResizeShadowWorkingBuffers())
        {
            shaderHelper.SetRenderShaderBuffers(renderShader);
            shaderHelper.SetPostProcessorBuffers(ppShader);
            shaderHelper.SetPostProcessorVariables(ppShader);
        }

        int2 fullThreads2D = new(renderTexture.width, renderTexture.height);
        int2 shadowThreads2D = GetShadowResolution();
        int shadowThreadsX = shadowThreads2D.x;

        if (ShadowType == ShadowType.None)
        {
            // No shadows — straight copy to PP
            ComputeHelper.DispatchKernel(ppShader, "ApplyWithoutShadows", fullThreads2D, ppShaderThreadSize2);
            return;
        }

        // Downsample the full-res source mask into the working low-res ShadowMask_dbA
        int kDown = ppShader.FindKernel("DownsampleShadowMask");
        if (kDown >= 0)
        {
            ComputeHelper.DispatchKernel(ppShader, "DownsampleShadowMask", shadowThreads2D, ppShaderThreadSize2);
        }

        // Create shadows at LOW RES (ray march)
        if (ShadowType == ShadowType.Vertical_Sharp || ShadowType == ShadowType.Vertical_Blurred)
        {
            // vertical kernel uses X dimension only (one ray per column)
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

        bool isBlurred =
            ShadowType == ShadowType.Vertical_Blurred ||
            ShadowType == ShadowType.Diagonal_Blurred ||
            ShadowType == ShadowType.Directional_Blurred;

        if (isBlurred)
        {
            // Work in low resolution for blur
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

    private void InitTimeSetRand()
    {
        renderShader.SetInt("LastTimeSetRand", Func.RandInt(0, 99999));
        timeSetRand = Func.RandInt(0, 99999);
        renderShader.SetInt("NextTimeSetRand", timeSetRand);
    }

    private void TryLoadAddressableCaustics()
    {
        if (_causticsLoadStarted) return;
        AssetReferenceT<Texture2DArray> addressableCausticsTexture = shaderHelper.addressableCausticsTexture;
        if (addressableCausticsTexture == null) return;
        if (!addressableCausticsTexture.RuntimeKeyIsValid()) return;

        _causticsLoadStarted = true;
        StartCoroutine(LoadAddressableCaustics_Coroutine());
    }

    private IEnumerator LoadAddressableCaustics_Coroutine()
    {
        // Wait 2s to let other processes run before loading the addressable.
        if (!PM.hasBeenReset) yield return new WaitForSecondsRealtime(2f);

        var init = Addressables.InitializeAsync();
        yield return init;

        _causticsHandle = shaderHelper.addressableCausticsTexture.LoadAssetAsync<Texture2DArray>();
        yield return _causticsHandle;

        if (_causticsHandle.Status == AsyncOperationStatus.Succeeded)
        {
            var newTex = _causticsHandle.Result;

            // Always set the field as requested
            precomputedCausticsTexture = newTex;

            // Maintain existing behavior: only apply to shader when in Precomputed mode
            if (CausticsType != CausticsType.Precomputed)
            {
                Debug.LogWarning("Addressable caustics texture loaded, but CausticsType in Main is NOT set to CausticsType.Precomputed.");
                yield break;
            }

            ApplyPrecomputedCausticsToShader(newTex);
        }
        else
        {
            Debug.LogError(_causticsHandle.OperationException ?? new System.Exception("Failed to load Addressable caustics texture."));
        }
    }

    private void ApplyPrecomputedCausticsToShader(Texture2DArray tex)
    {
        if (tex == null) { Debug.LogError("ApplyPrecomputedCausticsToShader texture is null."); return; }
        PrecomputedCausticsDims = new(tex.width, tex.height, tex.depth);
        renderShader.SetVector("PrecomputedCausticsDims", Utils.Int3ToVector3(PrecomputedCausticsDims));
        renderShader.SetTexture(1, "PrecomputedCaustics", tex);
    }

    public void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (simDevice == SimulationDevice.GPU) Graphics.Blit(ppRenderTexture, dest);
        else Graphics.Blit(src, dest);
    }

    private void OnDestroy()
    {
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
            // Shadows
            ShadowSrcFullRes,
            ShadowMask_dbA,
            ShadowMask_dbB,
            SharpShadowMask,
            ShadowDstMask,
            RimLightMask
        );

        if (_causticsHandle.IsValid()) Addressables.Release(_causticsHandle);
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

    // --- Helpers for dynamic material updates ---
    private void RecreateOrUpdateMaterialBuffer()
    {
        if (MaterialBuffer == null || MaterialBuffer.count != MaterialsCount)
        {
            ComputeHelper.CreateStructuredBuffer<Mat>(ref MaterialBuffer, Mats);
        }
        else
        {
            MaterialBuffer.SetData(Mats);
        }
    }
}using UnityEngine;

[CreateAssetMenu(menuName = "CustomMats/SimpleMat", fileName = "SimpleMat")]
public class SimpleMat : CustomMat
{
    public Texture2D colTexture;
}using Unity.Mathematics;
using UnityEngine;

public class CustomMat : ScriptableObject
{
    // Shader params
    public float3 baseColor = new(0.0f, 0.0f, 0.0f);
    [Range(0,1)] public float  opacity   = 1.0f;

    // UV transform / tiling: sign of colorTextureUpScaleFactor toggles mirror repeat (positive = mirror)
    public float2 sampleOffset = new(0, 0);
    public float  colorTextureUpScaleFactor = 1.0f;
    public bool   disableMirrorRepeat = false;

    // Tinting / edge color
    public float3 sampleColorMultiplier = new(1.0f, 1.0f, 1.0f);
    public bool   transparentEdges = false;
    public float3 edgeColor = new (1.0f, 1.0f, 1.0f);
}using UnityEngine;

[CreateAssetMenu(menuName = "CustomMats/RenderMat", fileName = "RenderMat")]
public class RenderMat : CustomMat
{
    [Header("Generation Settings")]
    public Material material;

    [Range(0, 10)] public float light;

    public Texture2D bakedTexture;
}using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Unity.VisualScripting;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class TextureBaker : MonoBehaviour
{
    // --- Capture controls (Play Mode only) ---
    public bool doUpdateRenderMats;
    public Camera targetCamera;

    // --- Sun light (Edit & Play) ---
    [Header("Sun Light")]
    [Tooltip("Directional/scene Light whose intensity will be set from RenderMat.light.")]
    public Light sunLight;

#if UNITY_EDITOR
    public Vector2Int resolution;

    [Tooltip("Pixels with alpha > this value are considered content. 0 = keep any non-zero alpha.")]
    [Range(0, 255)] public byte cropAlphaThreshold = 0;

    [Tooltip("Folder where screenshots are written. Files are named <RenderMatName>.png (or .jpg if already present).")]
    public string outputFolder = "Assets/Screenshots";

    // --- Prefab used to create children for RenderMats ---
    public GameObject childPrefab;           // Must have a MeshRenderer on the root or a child

    // --- Child visibility (Edit & Play) ---
    public Transform texturesRoot;           // Parent whose children are the “textures”
    public int visibleTextureIndex = -1;     // -1 = show none; else 0..last child

    int _lastVisibleIndex = int.MinValue;
    Transform _lastRoot;
    bool _suppressIndexWatcher;

    // --- RenderMat linkage (Edit & Play) ---
    public List<RenderMat> renderMats = new(); // One child per RenderMat, matched by name

    string _lastMatsSig = "";
    string _lastChildrenSig = "";

    void Start()
    {
        SyncChildrenToRenderMats();
        ApplyVisibleChild(visibleTextureIndex);

        // Batch update on start (play mode only)
        if (doUpdateRenderMats && Application.isPlaying && texturesRoot)
        {
            StartCoroutine(BatchUpdateRenderMats());
            Debug.Log("Baked all RenderMat textures.");
        }
    }

    void Update()
    {
        // Keep child set synced with RenderMats by NAME (edit & play)
        if (NeedsSync())
            SyncChildrenToRenderMats();

        // Visibility watcher (edit & play)
        if (!_suppressIndexWatcher && texturesRoot)
        {
            int max = texturesRoot.childCount - 1;
            int clamped = Mathf.Clamp(visibleTextureIndex, -1, max);
            if (clamped != visibleTextureIndex) visibleTextureIndex = clamped;

            if (texturesRoot != _lastRoot || visibleTextureIndex != _lastVisibleIndex)
                ApplyVisibleChild(visibleTextureIndex);
        }

        targetCamera.depth = (visibleTextureIndex > -1 && !Application.isPlaying) ? 5f : -1f;
    }

    // ---------------------- SYNC: RenderMats <-> Children (by NAME) ----------------------
    bool NeedsSync()
    {
        if (!texturesRoot) return false;
        var matNames = renderMats.Where(m => m).Select(m => m.name).OrderBy(n => n);
        var childNames = GetChildren(texturesRoot).Select(t => t.name).OrderBy(n => n);

        string matsSig = string.Join("|", matNames);
        string chSig = string.Join("|", childNames);

        return matsSig != _lastMatsSig || chSig != _lastChildrenSig;
    }

    void SyncChildrenToRenderMats()
    {
        if (!texturesRoot) return;

        // Build lookup of existing children by name
        var children = GetChildren(texturesRoot).ToList();
        var childMap = new Dictionary<string, Transform>(StringComparer.Ordinal);
        foreach (var c in children) childMap[c.name] = c;

        // Ensure each RenderMat has a child named exactly as the RenderMat
        foreach (var mat in renderMats)
        {
            if (!mat) continue;
            if (!childMap.TryGetValue(mat.name, out var child))
            {
                child = CreateChild(mat.name);
                childMap[mat.name] = child;
            }
            // Apply material slot 0
            ApplyMaterialToChild(child, mat.material);
        }

        // Remove any child that has no corresponding RenderMat name
        var matNames = new HashSet<string>(renderMats.Where(m => m).Select(m => m.name), StringComparer.Ordinal);
        foreach (var c in children)
        {
            if (!matNames.Contains(c.name))
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    DestroyImmediate(c.gameObject);
                else
                    Destroy(c.gameObject);
#else
                Destroy(c.gameObject);
#endif
            }
        }

        // Update signatures
        var matNamesNow = renderMats.Where(m => m).Select(m => m.name).OrderBy(n => n);
        var childNamesNow = GetChildren(texturesRoot).Select(t => t.name).OrderBy(n => n);
        _lastMatsSig = string.Join("|", matNamesNow);
        _lastChildrenSig = string.Join("|", childNamesNow);

        // Keep visibility index valid
        int max = texturesRoot.childCount - 1;
        if (visibleTextureIndex > max) visibleTextureIndex = max;
        if (max < 0) visibleTextureIndex = -1;
    }

    Transform CreateChild(string childName)
    {
        if (!texturesRoot) return null;

        GameObject go = null;
        if (childPrefab)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                go = (GameObject)PrefabUtility.InstantiatePrefab(childPrefab, texturesRoot);
            else
                go = Instantiate(childPrefab, texturesRoot);
#else
            go = Instantiate(childPrefab, texturesRoot);
#endif
        }
        else
        {
            go = new GameObject("Child");
            go.transform.SetParent(texturesRoot, false);
            if (!go.TryGetComponent<MeshRenderer>(out _)) go.AddComponent<MeshRenderer>();
            if (!go.TryGetComponent<MeshFilter>(out _)) go.AddComponent<MeshFilter>();
        }

        go.name = childName;
        go.transform.localPosition = new(0, 0, 100);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = new(75, 75, 1);

        return go.transform;
    }

    void ApplyMaterialToChild(Transform child, Material mat)
    {
        if (!child) return;
        var renderer = child.GetComponentInChildren<MeshRenderer>();
        if (!renderer) return;

#if UNITY_EDITOR
        var arr = !Application.isPlaying ? renderer.sharedMaterials : renderer.materials;
#else
        var arr = renderer.materials;
#endif
        if (arr == null || arr.Length == 0) arr = new Material[1];
        if (arr[0] != mat) arr[0] = mat;

#if UNITY_EDITOR
        if (!Application.isPlaying) renderer.sharedMaterials = arr;
        else renderer.materials = arr;
#else
        renderer.materials = arr;
#endif
    }

    static IEnumerable<Transform> GetChildren(Transform root)
    {
        for (int i = 0; i < root.childCount; i++) yield return root.GetChild(i);
    }

    // ---------------------- Visibility ----------------------
    public void ShowOnlyChild(int index)
    {
        if (!texturesRoot) return;
        int max = texturesRoot.childCount - 1;
        visibleTextureIndex = Mathf.Clamp(index, -1, max);
        ApplyVisibleChild(visibleTextureIndex);
    }

    void ApplyVisibleChild(int index)
    {
        _lastRoot = texturesRoot;
        _lastVisibleIndex = index;
        if (!texturesRoot) return;

        int count = texturesRoot.childCount;
        if (count == 0) return;

        if (index < 0)
        {
            for (int i = 0; i < count; i++)
                SetActive(texturesRoot.GetChild(i), false);
            return;
        }

        int clamped = Mathf.Clamp(index, 0, count - 1);
        for (int i = 0; i < count; i++)
            SetActive(texturesRoot.GetChild(i), i == clamped);

        // Update sun light intensity for the currently visible mat (preview)
        if (sunLight)
        {
            var child = texturesRoot.GetChild(clamped);
            var mat = renderMats.FirstOrDefault(m => m && m.name == child.name);
            ApplySunLightIntensity(mat);
        }
    }

    static void SetActive(Transform t, bool v)
    {
        if (t && t.gameObject.activeSelf != v) t.gameObject.SetActive(v);
    }

    // ---------------------- Sun light intensity ----------------------
    void ApplySunLightIntensity(RenderMat mat)
    {
        if (!sunLight || mat == null) return;
        // Assumes RenderMat exposes "public float light" which we use as the intensity value.
        sunLight.intensity = mat.light;
    }

    // ---------------------- Batch update (Play Mode) ----------------------
    IEnumerator BatchUpdateRenderMats()
    {
        SyncChildrenToRenderMats();

        int originalIndex = visibleTextureIndex;
        _suppressIndexWatcher = true;

        float prevLightIntensity = sunLight ? sunLight.intensity : 0f;

        foreach (var mat in renderMats)
        {
            if (!mat) continue;

            var child = FindChildByName(mat.name);
            if (!child) continue;

            int idx = child.GetSiblingIndex();
            visibleTextureIndex = idx;
            ApplyVisibleChild(idx); // also updates sun light intensity for preview

            // Ensure intensity is set for this mat before capture
            if (sunLight) ApplySunLightIntensity(mat);

            yield return null;
            yield return new WaitForEndOfFrame();

            Texture2D savedAsset = null;
            yield return StartCoroutine(CaptureAndSaveCoroutine(mat.name, t => savedAsset = t));

#if UNITY_EDITOR
            if (savedAsset)
            {
                mat.bakedTexture = savedAsset;
                EditorUtility.SetDirty(mat);
            }
#endif
        }

#if UNITY_EDITOR
        AssetDatabase.SaveAssets();
#endif

        visibleTextureIndex = originalIndex;
        ApplyVisibleChild(visibleTextureIndex);
        _suppressIndexWatcher = false;

        // Restore original light intensity after batch
        if (sunLight) sunLight.intensity = prevLightIntensity;
    }

    Transform FindChildByName(string n)
    {
        if (!texturesRoot) return null;
        for (int i = 0; i < texturesRoot.childCount; i++)
        {
            var c = texturesRoot.GetChild(i);
            if (string.Equals(c.name, n, StringComparison.Ordinal)) return c;
        }
        return null;
    }

    // ---------------------- Capture (Play Mode) ----------------------
    IEnumerator CaptureAndSaveCoroutine(string baseName, Action<Texture2D> onSavedAsset = null)
    {
        var cam = targetCamera ? targetCamera : GetComponent<Camera>();
        if (!cam) yield break;

        int w = resolution.x > 0 ? resolution.x : (Application.isPlaying && cam.pixelWidth > 0 ? cam.pixelWidth : 1024);
        int h = resolution.y > 0 ? resolution.y : (Application.isPlaying && cam.pixelHeight > 0 ? cam.pixelHeight : 1024);

        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var prevActive = RenderTexture.active;

        cam.targetTexture = rt;
        yield return null;
        yield return new WaitForEndOfFrame();

        RenderTexture.active = rt;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
        tex.Apply(false, false);

        // Crop handling
        Texture2D toSave = tex;
        if (TryGetOpaqueBounds(tex, cropAlphaThreshold, out var bounds))
        {
            if (!(bounds.width == w && bounds.height == h))
            {
                toSave = CropTexture(tex, bounds);
            }
        }

        cam.targetTexture = prevTarget;
        RenderTexture.active = prevActive;

#if UNITY_EDITOR
        Texture2D asset = null;
        asset = SaveTextureAsset(toSave, baseName);
        onSavedAsset?.Invoke(asset);
#else
        onSavedAsset?.Invoke(null);
#endif

        // Cleanup
        if (toSave != tex) Destroy(toSave);
        Destroy(rt);
        Destroy(tex);
    }

    // --- Helpers: alpha crop ---
    static bool TryGetOpaqueBounds(Texture2D tex, byte alphaThreshold, out RectInt bounds)
    {
        int w = tex.width;
        int h = tex.height;
        var pixels = tex.GetPixels32();

        int minX = w, minY = h, maxX = -1, maxY = -1;

        // Find tightest bounding box for alpha > threshold
        for (int y = 0; y < h; y++)
        {
            int row = y * w;
            for (int x = 0; x < w; x++)
            {
                if (pixels[row + x].a > alphaThreshold)
                {
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
        }

        if (maxX < minX || maxY < minY)
        {
            bounds = new RectInt(0, 0, 0, 0);
            return false; // all transparent
        }

        int cw = maxX - minX + 1;
        int ch = maxY - minY + 1;
        bounds = new RectInt(minX, minY, cw, ch);
        return true;
    }

    static Texture2D CropTexture(Texture2D src, RectInt rect)
    {
        int w = rect.width;
        int h = rect.height;
        var srcPixels = src.GetPixels32();
        var dstPixels = new Color32[w * h];

        int srcW = src.width;

        for (int y = 0; y < h; y++)
        {
            int srcY = rect.y + y;
            int srcRow = srcY * srcW;
            int dstRow = y * w;
            for (int x = 0; x < w; x++)
            {
                dstPixels[dstRow + x] = srcPixels[srcRow + rect.x + x];
            }
        }

        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false);
        dst.SetPixels32(dstPixels);
        dst.Apply(false, false);
        return dst;
    }

#if UNITY_EDITOR
    // ---------------------- Editor helpers ----------------------
    Texture2D SaveTextureAsset(Texture2D tex, string baseName)
    {
        // Ensure folder exists and is inside Assets
        string folder = !string.IsNullOrEmpty(outputFolder) && outputFolder.StartsWith("Assets")
            ? outputFolder : "Assets/Screenshots";
        Directory.CreateDirectory(folder);

        // Name exactly after the RenderMat
        string safe = Sanitize(string.IsNullOrEmpty(baseName) ? name : baseName);

        // If a file with this base name already exists, reuse its extension to keep the same GUID/meta.
        // Otherwise, default to PNG.
        string[] candidateExts = { ".png", ".jpg", ".jpeg" };
        string chosenExt = null;
        string path = null;
        foreach (var ext in candidateExts)
        {
            var p = Path.Combine(folder, safe + ext).Replace("\\", "/");
            if (File.Exists(p))
            {
                chosenExt = ext;
                path = p;
                break;
            }
        }
        if (path == null)
        {
            chosenExt = ".png";
            path = Path.Combine(folder, safe + chosenExt).Replace("\\", "/");
        }

        // Encode accordingly and overwrite
        byte[] bytes = (chosenExt.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
                        chosenExt.Equals(".jpeg", StringComparison.OrdinalIgnoreCase))
                        ? tex.EncodeToJPG(95)
                        : tex.EncodeToPNG();

        File.WriteAllBytes(path, bytes);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.isReadable = true; // read/write
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.sRGBTexture = true;
            EditorUtility.SetDirty(importer);
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static string Sanitize(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return string.IsNullOrEmpty(s) ? "Baked" : s;
    }
#endif
#else
    void Awake()
    {
        var cam = targetCamera ? targetCamera : GetComponent<Camera>();
        cam.depth = -1f;
    }
#endif
}using System;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using Unity.Mathematics;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public int MaxAtlasDims;

    private Vector2 sceneMin;
    private Vector2 sceneMax;
    private bool referencesHaveBeenSet = false;

    private Transform sensorUIContainer;
    private Transform sensorOutlineContainer;
    private Main main;
    private ArrowManager arrowManager;
    private SensorManager sensorManager;

    private void SetReferences()
    {
        sensorUIContainer = GameObject.FindGameObjectWithTag("SensorUIContainer").GetComponent<Transform>();
        sensorOutlineContainer = GameObject.FindGameObjectWithTag("SensorOutlineContainer").GetComponent<Transform>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        arrowManager = GameObject.FindGameObjectWithTag("ArrowManager").GetComponent<ArrowManager>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();

        referencesHaveBeenSet = true;
    }

    public int2 GetBounds(int maxInfluenceRadius)
    {
        int2 bounds = new(Mathf.CeilToInt(transform.localScale.x), Mathf.CeilToInt(transform.localScale.y));
        int2 boundsMod = bounds % maxInfluenceRadius;
        if (boundsMod.x != 0) bounds.x += maxInfluenceRadius - boundsMod.x;
        if (boundsMod.y != 0) bounds.y += maxInfluenceRadius - boundsMod.y;
        return bounds;
    }

    public bool IsPointInsideBounds(Vector2 point)
    {
        if (!referencesHaveBeenSet) SetReferences();

        sceneMin.x = transform.position.x - transform.localScale.x * 0.5f + main.FluidPadding;
        sceneMin.y = transform.position.y - transform.localScale.y * 0.5f + main.FluidPadding;
        sceneMax.x = transform.position.x + transform.localScale.x * 0.5f - main.FluidPadding;
        sceneMax.y = transform.position.y + transform.localScale.y * 0.5f - main.FluidPadding;

        bool isInsideBounds = point.x > sceneMin.x
                              && point.y > sceneMin.y
                              && point.x < sceneMax.x
                              && point.y < sceneMax.y;

        return isInsideBounds;
    }

    public bool IsSpaceEmpty(Vector2 point, SceneFluid thisFluid, SceneRigidBody[] allRigidBodies, SceneFluid[] allFluids)
    {
        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            ColliderType colliderType = rigidBody.rbInput.colliderType;
            bool isFluidCollider = colliderType == ColliderType.Fluid || colliderType == ColliderType.All;
            if (rigidBody.IsPointInsidePolygon(point) && isFluidCollider) return false;
        }

        SceneFluid[] sortedFluids = allFluids
            .OrderBy(fluid => fluid.transform.GetSiblingIndex())
            .ToArray();

        int thisFluidIndex = Array.IndexOf(sortedFluids, thisFluid);
        for (int i = 0; i < thisFluidIndex; i++)
        {
            if (sortedFluids[i].IsPointInsidePolygon(point)) return false;
        }

        return true;
    }

    // ===============================
    // UPDATED: supports any CustomMat[]
    // ===============================
    public (Texture2D, Mat[]) ConstructTextureAtlas(CustomMat[] materials)
    {
        // Collect the color textures (from either SimpleMat.coltex or RenderMat.bakedTexture)
        List<Texture2D> textures = new();
        List<int> mapping = new(); // rect index -> material index

        for (int i = 0; i < materials.Length; i++)
        {
            Texture2D colTex = GetColTexture(materials[i]);
            if (colTex != null)
            {
                if (!colTex.isReadable)
                    Debug.LogWarning("Texture " + colTex.name + " is not readable. Enable Read/Write.");
                textures.Add(colTex);
                mapping.Add(i);
            }
        }

        // Build the atlas
        Texture2D atlas = new(MaxAtlasDims, MaxAtlasDims, TextureFormat.RGBAHalf, false);
        Rect[] rects = textures.Count > 0
            ? atlas.PackTextures(textures.ToArray(), 1, MaxAtlasDims)
            : Array.Empty<Rect>();

        float sizeMB = (atlas.width * atlas.height * 8f) / (1024f * 1024f);
        StringUtils.LogIfInEditor($"Texture atlas (colTex) with {rects.Length} sub-textures, size {sizeMB:0.00} MB");

        // Helpers to convert rects to atlas-space int2 coords/dims
        int2 GetTexLoc(Rect rect)  => new((int)(rect.x * atlas.width), (int)(rect.y * atlas.height));
        int2 GetTexDims(Rect rect) => new((int)(rect.width * atlas.width), (int)(rect.height * atlas.height));

        // For each material, record the colTex rect (or mark as missing)
        Rect[] colRects = Enumerable.Repeat(new Rect(0, 0, 0, 0), materials.Length).ToArray();

        for (int i = 0; i < mapping.Count; i++)
        {
            int matIndex = mapping[i];
            colRects[matIndex] = rects[i];
        }

        // Build render materials (Mat) array
        Mat[] renderMats = new Mat[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            CustomMat bm = materials[i];

            bool hasCol = colRects[i].width > 0f;
            int2 colLoc  = hasCol ? GetTexLoc(colRects[i])  : new int2(-1, -1);
            int2 colDims = hasCol ? GetTexDims(colRects[i]) : new int2(-1, -1);

            renderMats[i] = InitMat(
                bm,
                bm != null ? bm.baseColor : new float3(0,0,0),
                colLoc, colDims,
                bm != null ? bm.sampleOffset : new float2(0,0)
            );
        }

        return (atlas, renderMats);
    }

    private static Texture2D GetColTexture(CustomMat bm)
    {
        if (bm == null) return null;

        if (bm is SimpleMat mi && mi.colTexture != null)
            return mi.colTexture;

        if (bm is RenderMat rm && rm.bakedTexture != null)
            return rm.bakedTexture;

        return null;
    }

    // UPDATED: now accepts CustomMat instead of SimpleMat
    private Mat InitMat(CustomMat CustomMat,
                        float3 baseCol,
                        int2 colTexLoc, int2 colTexDims,
                        float2 sampleOffset)
    {
        float upScale = (CustomMat != null) ? CustomMat.colorTextureUpScaleFactor : 1.0f;
        bool disableMirror = (CustomMat != null) && CustomMat.disableMirrorRepeat;

        return new Mat
        {
            colTexLoc = colTexLoc,
            colTexDims = colTexDims,

            sampleOffset = sampleOffset,
            colTexUpScaleFactor = disableMirror ? -upScale : upScale,

            baseCol = baseCol,
            opacity = Mathf.Clamp(CustomMat != null ? CustomMat.opacity : 1.0f, 0.0f, 1.0f),
            sampleColMul = CustomMat != null ? CustomMat.sampleColorMultiplier : new float3(1,1,1),
            edgeCol = (CustomMat != null && CustomMat.transparentEdges) ? new float3(-1, -1, -1) : (CustomMat != null ? CustomMat.edgeColor : new float3(0,0,0))
        };
    }

    public PData[] GenerateParticles(int maxParticlesNum, float gridSpacing = 0)
    {
        if (maxParticlesNum == 0) return new PData[0];

        SceneFluid[] allFluids = GetAllSceneFluids();
        Vector2 offset = GetBoundsOffset();

        List<PData> allPDatas = new();
        foreach (SceneFluid fluid in allFluids)
        {
            PData[] pDatas = fluid.GenerateParticles(offset, gridSpacing);
            foreach (var pData in pDatas)
            {
                allPDatas.Add(pData);
                if (--maxParticlesNum <= 0) return allPDatas.ToArray();
            }
        }

        return allPDatas.ToArray();
    }

    public (RBData[], RBVector[], SensorArea[]) CreateRigidBodies(float? rbCalcGridSpacingInput = null)
    {
        float rbCalcGridSpacing = rbCalcGridSpacingInput ?? 1.0f;
        if (!referencesHaveBeenSet) SetReferences();

        SceneRigidBody[] allRigidBodies = GetAllSceneRigidBodies();

        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            rigidBody.ComputeCentroid(rbCalcGridSpacing);
        }

        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            if (rigidBody.polygonCollider == null)
            {
                rigidBody.polygonCollider = rigidBody.GetComponent<PolygonCollider2D>();
            }

            int pathCount = rigidBody.polygonCollider.pathCount;
            for (int p = 0; p < pathCount; p++)
            {
                Vector2[] pathPoints = rigidBody.polygonCollider.GetPath(p);
                pathPoints = ArrayUtils.RemoveAdjacentDuplicates(pathPoints);
                rigidBody.polygonCollider.SetPath(p, pathPoints);
            }
        }

        Vector2 boundsOffset = GetBoundsOffset();

        List<RBData> allRBData = new();
        List<RBVector> allRBVectors = new();
        List<SensorBase> sensors = new();

        for (int i = 0; i < allRigidBodies.Length; i++)
        {
            SceneRigidBody rigidBody = allRigidBodies[i];
            if (!rigidBody.rbInput.includeInSimulation) continue;

            Transform transform = rigidBody.transform;
            Vector2 parentOffset = transform.position - transform.localPosition;

            Vector2[] vectors = GetTransformedMultiPathPoints(rigidBody, boundsOffset, out Vector2 transformedRBPos);
            if (rigidBody.addInBetweenPoints)
            {
                AddInBetweenPoints(ref vectors, rigidBody.doRecursiveSubdivisison, rigidBody.minDstForSubDivision);
            }

            (float inertia, float maxRadiusSqr) = rigidBody.ComputeInertiaAndBalanceRigidBody(
                ref vectors, ref transformedRBPos, boundsOffset, rbCalcGridSpacing
            );

            RBInput rbInput = rigidBody.rbInput;
            int springLinkedRBIndex = rbInput.linkedRigidBody == null
                ? -1
                : Array.IndexOf(allRigidBodies, rbInput.linkedRigidBody);

            if (rbInput.constraintType == ConstraintType.Spring && springLinkedRBIndex == -1)
            {
                Debug.LogError("Linked rigid body not set. SceneRigidBody: " + rigidBody.name);
            }
            else if (i == springLinkedRBIndex)
            {
                Debug.LogWarning("Spring to self. Removed.");
                rbInput.constraintType = ConstraintType.None;
            }

            float springRestLength = rbInput.springRestLength;
            if (rbInput.linkedRigidBody != null && rbInput.autoSpringRestLength)
            {
                float distance = Vector2.Distance(
                    rigidBody.cachedCentroid + rbInput.localLinkPosThisRB,
                    rbInput.linkedRigidBody.cachedCentroid + rbInput.localLinkPosOtherRB
                );
                springRestLength = distance;
            }

            int startIndex = allRBVectors.Count;
            foreach (Vector2 v in vectors)
            {
                allRBVectors.Add(new RBVector(v, i));
            }
            int endIndex = allRBVectors.Count - 1;

            allRBData.Add(InitRBData(
                rbInput,
                inertia,
                maxRadiusSqr,
                springLinkedRBIndex,
                springRestLength,
                startIndex,
                endIndex,
                transformedRBPos,
                parentOffset
            ));

            foreach (SensorBase sensor in rigidBody.linkedSensors)
            {
                if (sensor == null) continue;
                if (!sensor.isActiveAndEnabled) continue;

                if (sensors.Contains(sensor))
                {
                    Debug.LogWarning("Duplicate sensor " + sensor.name);
                }
                else
                {
                    if (sensor is RigidBodySensor rigidBodySensor)
                    {
                        rigidBodySensor.linkedRBIndex = i;
                        sensors.Add(sensor);

                        rigidBodySensor.SetReferences(sensorUIContainer, sensorOutlineContainer, main, sensorManager);
                        rigidBodySensor.Initialize(transformedRBPos);
                    }
                    else if (sensor is RigidBodyArrow rigidBodyArrow)
                    {
                        rigidBodyArrow.linkedRBIndex = i;
                        sensors.Add(sensor);

                        rigidBodyArrow.SetReferences(arrowManager, main, sensorManager);
                        rigidBodyArrow.Initialize();
                    }
                }
            }
        }

        List<SensorArea> sensorAreas = new();

        GameObject[] fluidSensorObjects = GameObject.FindGameObjectsWithTag("FluidSensor");
        FluidSensor[] fluidSensors = Array.ConvertAll(fluidSensorObjects, obj => obj.GetComponent<FluidSensor>());
        foreach (FluidSensor fluidSensor in fluidSensors)
        {
            if (fluidSensor == null) continue;
            sensors.Add(fluidSensor);

            fluidSensor.SetReferences(sensorUIContainer, sensorOutlineContainer, main, sensorManager);
            fluidSensor.Initialize(Vector2.zero);

            sensorAreas.Add(fluidSensor.GetSensorAreaData());
        }

        GameObject[] fluidArrowFieldObjects = GameObject.FindGameObjectsWithTag("FluidArrowField");
        FluidArrowField[] fluidArrowFields = Array.ConvertAll(fluidArrowFieldObjects, obj => obj.GetComponent<FluidArrowField>());
        foreach (FluidArrowField fluidArrowField in fluidArrowFields)
        {
            if (fluidArrowField == null) continue;
            sensors.Add(fluidArrowField);

            fluidArrowField.SetReferences(arrowManager, main, sensorManager);
            fluidArrowField.Initialize();

            if (fluidArrowField.doRenderMeasurementZone) sensorAreas.Add(fluidArrowField.GetSensorAreaData());
        }

        sensorManager.sensors = sensors;

        return (allRBData.ToArray(), allRBVectors.ToArray(), sensorAreas.ToArray());
    }

    private Vector2[] GetTransformedMultiPathPoints(SceneRigidBody rigidBody, Vector2 offset, out Vector2 transformedRBPos)
    {
        List<Vector2> combined = new();
        PolygonCollider2D poly = rigidBody.GetComponent<PolygonCollider2D>();

        for (int p = 0; p < poly.pathCount; p++)
        {
            Vector2[] pathPoints = poly.GetPath(p);

            for (int i = 0; i < pathPoints.Length; i++)
            {
                Vector2 worldPt = rigidBody.transform.TransformPoint(pathPoints[i]);
                if (p > 0 && i == 0) worldPt.x += Main.PathFlagOffset;
                combined.Add(worldPt);
            }
        }

        transformedRBPos = (Vector2)rigidBody.transform.position + offset;
        for (int i = 0; i < combined.Count; i++)
        {
            combined[i] = combined[i] + offset - transformedRBPos;
        }

        return combined.ToArray();
    }

    public static void AddInBetweenPoints(ref Vector2[] vectors, bool doRecursiveSubdivisison, float minDst)
    {
        if (doRecursiveSubdivisison)
        {
            minDst = Mathf.Max(minDst, 0.5f);
            AddInBetweenPointsRecursively(ref vectors, minDst);
        }
        else
        {
            int numVectors = vectors.Length;
            List<Vector2> newVectors = new();

            int pathStartIndex = 0;
            Vector2 firstPathVec = vectors[0];
            Vector2 lastVec = firstPathVec;
            newVectors.Add(lastVec);

            for (int i = 1; i <= numVectors; i++)
            {
                bool endOfArray = i == numVectors;
                int vecIndex = endOfArray ? pathStartIndex : i;
                Vector2 nextVec = vectors[vecIndex];

                Vector2 inBetween;
                bool newPathFlag = nextVec.x > Main.PathFlagThreshold;
                if (newPathFlag)
                {
                    nextVec.x -= Main.PathFlagOffset;
                    float randOffset = UnityEngine.Random.Range(-0.05f, 0.05f);
                    inBetween = (lastVec * (1 + randOffset) + firstPathVec * (1 - randOffset)) / 2.0f;
                    firstPathVec = nextVec;
                    lastVec = nextVec;
                    pathStartIndex = vecIndex;
                    nextVec.x += Main.PathFlagOffset;
                }
                else
                {
                    inBetween = (lastVec + nextVec) / 2.0f;
                    lastVec = nextVec;
                }

                newVectors.Add(inBetween);
                if (!endOfArray) newVectors.Add(nextVec);
            }

            vectors = newVectors.ToArray();
        }
    }

    private static void AddInBetweenPointsRecursively(ref Vector2[] vectors, float minDst)
    {
        bool needsSubdivision = false;
        List<Vector2> newVectors = new();

        int count = vectors.Length;
        for (int i = 0; i < count; i++)
        {
            Vector2 current = vectors[i];
            Vector2 next = vectors[(i + 1) % count];

            newVectors.Add(current);

            bool currentIsMarker = current.x > Main.PathFlagThreshold;
            bool nextIsMarker = next.x > Main.PathFlagThreshold;

            if (!currentIsMarker && !nextIsMarker)
            {
                float distance = Vector2.Distance(current, next);
                if (distance > minDst)
                {
                    Vector2 inBetween = (current + next) / 2f;
                    newVectors.Add(inBetween);
                    needsSubdivision = true;
                }
            }
        }

        vectors = newVectors.ToArray();

        if (needsSubdivision)
        {
            AddInBetweenPointsRecursively(ref vectors, minDst);
        }
    }

    private Vector2 GetBoundsOffset()
    {
        return new Vector2(
            transform.localScale.x * 0.5f - transform.position.x,
            transform.localScale.y * 0.5f - transform.position.y
        );
    }

    public static SceneRigidBody[] GetAllSceneRigidBodies()
    {
        List<GameObject> rigidBodyObjects = GameObject.FindGameObjectsWithTag("RigidBody").ToList();
        List<SceneRigidBody> validRigidBodies = new();
        foreach (GameObject rigidBodyObject in rigidBodyObjects)
        {
            SceneRigidBody rb = rigidBodyObject.GetComponent<SceneRigidBody>();
            if (rb.rbInput.includeInSimulation) validRigidBodies.Add(rb);
        }
        return validRigidBodies.ToArray();
    }

    public static SceneFluid[] GetAllSceneFluids()
    {
        GameObject[] fluidObjects = GameObject.FindGameObjectsWithTag("Fluid");
        SceneFluid[] allFluids = new SceneFluid[fluidObjects.Length];
        for (int i = 0; i < fluidObjects.Length; i++)
        {
            allFluids[i] = fluidObjects[i].GetComponent<SceneFluid>();
        }
        return allFluids;
    }

    private RBData InitRBData(RBInput rbInput,
                              float inertia,
                              float maxRadiusSqr,
                              int linkedRBIndex,
                              float springRestLength,
                              int startIndex,
                              int endIndex,
                              Vector2 pos,
                              Vector2 parentOffset)
    {
        bool canMove = rbInput.canMove && rbInput.constraintType != ConstraintType.LinearMotor;
        bool isRBCollider = rbInput.colliderType == ColliderType.RigidBody || rbInput.colliderType == ColliderType.All;
        bool isFluidCollider = rbInput.colliderType == ColliderType.Fluid || rbInput.colliderType == ColliderType.All;
        bool isLinearMotor = rbInput.constraintType == ConstraintType.LinearMotor;
        bool isRigidConstraint = rbInput.constraintType == ConstraintType.Rigid;
        bool isSpringConstraint = rbInput.constraintType == ConstraintType.Spring;

        int stateFlags = 0;
        Func.SetBit(ref stateFlags, 0, false);
        Func.SetBit(ref stateFlags, 1, rbInput.disallowBorderCollisions);

        return new RBData
        {
            pos = pos,
            vel_AsInt2 = rbInput.canMove ? Func.Float2AsInt2(rbInput.velocity, main.FloatIntPrecisionRB) : 0,
            nextPos = 0,
            nextVel = 0,
            rotVel_AsInt = rbInput.canRotate
                ? Func.FloatAsInt(rbInput.angularVelocity, 500000.0f)
                : 0,
            totRot = 0,
            mass = canMove
                ? rbInput.mass
                : (isLinearMotor
                    ? (rbInput.doRoundTrip ? -2 : -1)
                    : 0),
            inertia = rbInput.canRotate ? inertia : 0,
            gravity = rbInput.gravity,

            rbElasticity = isRBCollider ? Mathf.Max(rbInput.rbElasticity, 0.05f) : -1,
            fluidElasticity = isFluidCollider ? Mathf.Max(rbInput.fluidElasticity, 0.05f) : -1,
            friction = rbInput.friction,
            passiveDamping = rbInput.passiveDamping,

            maxRadiusSqr = rbInput.isInteractable ? maxRadiusSqr : -maxRadiusSqr,

            startIndex = startIndex,
            endIndex = endIndex,

            linkedRBIndex = (isSpringConstraint || isRigidConstraint) ? linkedRBIndex : -1,
            springRestLength = isRigidConstraint ? 0 : springRestLength,
            springStiffness = isRigidConstraint ? 0 : rbInput.springStiffness,
            damping = isRigidConstraint ? 0 : rbInput.damping,

            localLinkPosThisRB = isLinearMotor
                ? rbInput.startPos + parentOffset
                : rbInput.localLinkPosThisRB,
            localLinkPosOtherRB = isLinearMotor
                ? rbInput.endPos + parentOffset
                : rbInput.localLinkPosOtherRB,

            lerpSpeed = isLinearMotor ? rbInput.lerpSpeed : 0,
            lerpTimeOffset = rbInput.lerpTimeOffset,

            heatingStrength = rbInput.heatingStrength,
            recordedSpringForce = 0,
            recordedFrictionForce = 0,

            renderPriority = rbInput.disableRender ? -1 : rbInput.renderPriority,
            matIndex = rbInput.matIndex,
            springMatIndex = rbInput.disableSpringRender ? -1 : rbInput.springMatIndex,
            stateFlags = stateFlags
        };
    }
}using Resources2;
using UnityEngine;
using UnityEngine.AddressableAssets;
using PM = ProgramManager;

public class ShaderHelper : MonoBehaviour
{
    public Main m;
    public AssetReferenceT<Texture2DArray> addressableCausticsTexture;

    public void SetPSimShaderBuffers(ComputeShader pSimShader)
    {
        // Kernel PreCalculations
        pSimShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(0, "PTypes", m.PTypeBuffer);

        // Kernel PreCalculations
        pSimShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);

        pSimShader.SetBuffer(1, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(1, "PTypes", m.PTypeBuffer);

        pSimShader.SetBuffer(2, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

        // Kernel TransferAllSpringData - 8/8 buffers
        pSimShader.SetBuffer(3, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(3, "PTypes", m.PTypeBuffer);
        pSimShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
        pSimShader.SetBuffer(3, "SpringCapacities", m.SpringCapacitiesBuffer);
        pSimShader.SetBuffer(3, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        pSimShader.SetBuffer(3, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        pSimShader.SetBuffer(3, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

        // Kernel ParticleForces - 8/8 buffers
        pSimShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);

        pSimShader.SetBuffer(4, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(4, "PTypes", m.PTypeBuffer);

        pSimShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);
        pSimShader.SetBuffer(4, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        pSimShader.SetBuffer(4, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        pSimShader.SetBuffer(4, "ParticleSpringsCombined", m.ParticleSpringsCombinedBuffer);

        pSimShader.SetBuffer(5, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(5, "PTypes", m.PTypeBuffer);
        pSimShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);

        pSimShader.SetBuffer(6, "RecordedFluidDatas", m.RecordedFluidDataBuffer);

        pSimShader.SetBuffer(7, "SpatialLookup", m.SpatialLookupBuffer);
        pSimShader.SetBuffer(7, "PDatas", m.PDataBuffer);
        pSimShader.SetBuffer(7, "PTypes", m.PTypeBuffer);
        pSimShader.SetBuffer(7, "RecordedFluidDatas", m.RecordedFluidDataBuffer);
    }

    public void SetRenderShaderBuffers(ComputeShader renderShader)
    {
        // NOTE: The render shader writes the full-resolution source shadow mask (ShadowMask) which
        //       will be downsampled in the PP shader. Always bind the FULL-RES buffer here.
        renderShader.SetBuffer(0, "ShadowMask", m.ShadowSrcFullRes);

        if (m.ParticlesNum != 0)
        {
            renderShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);
            renderShader.SetBuffer(1, "StartIndices", m.StartIndicesBuffer);
            renderShader.SetBuffer(1, "PDatas", m.PDataBuffer);
            renderShader.SetBuffer(1, "PTypes", m.PTypeBuffer);
            renderShader.SetBuffer(1, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(1, "SensorAreas", m.SensorAreaBuffer);
            renderShader.SetBuffer(1, "ShadowMask", m.ShadowSrcFullRes);
        }

        if (m.NumRigidBodies != 0)
        {
            renderShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(2, "RBVectors", m.RBVectorBuffer);
            renderShader.SetBuffer(2, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(2, "ShadowMask", m.ShadowSrcFullRes);

            renderShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
            renderShader.SetBuffer(3, "Materials", m.MaterialBuffer);
            renderShader.SetBuffer(3, "ShadowMask", m.ShadowSrcFullRes);
        }
    }

    public void SetRenderShaderTextures(ComputeShader renderShader)
    {
        renderShader.SetTexture(0, "Result", m.renderTexture);
        renderShader.SetTexture(0, "Background", m.backgroundTexture);

        renderShader.SetTexture(1, "Result", m.renderTexture);
        renderShader.SetTexture(1, "DynamicCaustics", m.dynamicCausticsTexture);
        if (m.precomputedCausticsTexture != null) renderShader.SetTexture(1, "PrecomputedCaustics", m.precomputedCausticsTexture);
        renderShader.SetTexture(1, "LiquidVelocityGradient", m.LiquidVelocityGradientTexture);
        renderShader.SetTexture(1, "GasVelocityGradient", m.GasVelocityGradientTexture);
        renderShader.SetTexture(1, "Background", m.backgroundTexture);
        renderShader.SetTexture(1, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(2, "Result", m.renderTexture);
        renderShader.SetTexture(2, "Background", m.backgroundTexture);
        renderShader.SetTexture(2, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(3, "Result", m.renderTexture);
        renderShader.SetTexture(3, "Atlas", m.AtlasTexture);

        renderShader.SetTexture(4, "Result", m.renderTexture);
        renderShader.SetTexture(4, "UITexture", m.uiTexture);
    }

    public void SetPostProcessorBuffers(ComputeShader ppShader)
    {
        // Find kernels
        int kCreateV    = ppShader.FindKernel("CreateShadowsVertical");
        int kCreateD    = ppShader.FindKernel("CreateShadowsDiagonal");
        int kCreateDir  = ppShader.FindKernel("CreateShadowsDirectional");

        int kDownsample = ppShader.FindKernel("DownsampleShadowMask");

        int kCopySharp  = ppShader.FindKernel("CopySharpShadows");
        int kBlurGauss  = ppShader.FindKernel("BlurShadowsGaussian");
        int kBlurBox    = ppShader.FindKernel("BlurShadowsBox");

        int kApplySharp = ppShader.FindKernel("ApplySharpShadows");
        int kApplyBlur  = ppShader.FindKernel("ApplyBlurredShadows");
        int kApplyNo    = ppShader.FindKernel("ApplyWithoutShadows");

        // --- CreateShadowsVertical ---
        ppShader.SetBuffer(kCreateV, "ShadowMask_dbA",  m.ShadowMask_dbA);
        ppShader.SetBuffer(kCreateV, "ShadowMask_dbB",  m.ShadowMask_dbB);
        ppShader.SetBuffer(kCreateV, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(kCreateV, "ShadowDstMask",   m.ShadowDstMask);
        ppShader.SetBuffer(kCreateV, "RimLightMask",    m.RimLightMask);

        // --- CreateShadowsDiagonal ---
        ppShader.SetBuffer(kCreateD, "ShadowMask_dbA",  m.ShadowMask_dbA);
        ppShader.SetBuffer(kCreateD, "ShadowMask_dbB",  m.ShadowMask_dbB);
        ppShader.SetBuffer(kCreateD, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(kCreateD, "ShadowDstMask",   m.ShadowDstMask);
        ppShader.SetBuffer(kCreateD, "RimLightMask",    m.RimLightMask);

        // --- CreateShadowsDirectional ---
        ppShader.SetBuffer(kCreateDir, "ShadowMask_dbA",  m.ShadowMask_dbA);
        ppShader.SetBuffer(kCreateDir, "ShadowMask_dbB",  m.ShadowMask_dbB);
        ppShader.SetBuffer(kCreateDir, "SharpShadowMask", m.SharpShadowMask);
        ppShader.SetBuffer(kCreateDir, "ShadowDstMask",   m.ShadowDstMask);
        ppShader.SetBuffer(kCreateDir, "RimLightMask",    m.RimLightMask);

        // --- DownsampleShadowMask ---
        ppShader.SetBuffer(kDownsample, "ShadowSrcFullRes", m.ShadowSrcFullRes);
        ppShader.SetBuffer(kDownsample, "ShadowMask_dbA",   m.ShadowMask_dbA);
        ppShader.SetBuffer(kDownsample, "ShadowMask_dbB",   m.ShadowMask_dbB);
        ppShader.SetBuffer(kDownsample, "SharpShadowMask",  m.SharpShadowMask);
        ppShader.SetBuffer(kDownsample, "ShadowDstMask",    m.ShadowDstMask);
        ppShader.SetBuffer(kDownsample, "RimLightMask",     m.RimLightMask);

        // --- CopySharpShadows ---
        ppShader.SetBuffer(kCopySharp, "ShadowMask_dbA",   m.ShadowMask_dbA);
        ppShader.SetBuffer(kCopySharp, "SharpShadowMask",  m.SharpShadowMask);
        ppShader.SetBuffer(kCopySharp, "ShadowMask_dbB",   m.ShadowMask_dbB);
        ppShader.SetBuffer(kCopySharp, "ShadowDstMask",    m.ShadowDstMask);
        ppShader.SetBuffer(kCopySharp, "RimLightMask",     m.RimLightMask);

        // --- BlurShadowsGaussian ---
        ppShader.SetBuffer(kBlurGauss, "ShadowMask_dbA",   m.ShadowMask_dbA);
        ppShader.SetBuffer(kBlurGauss, "ShadowMask_dbB",   m.ShadowMask_dbB);
        ppShader.SetBuffer(kBlurGauss, "SharpShadowMask",  m.SharpShadowMask);
        ppShader.SetBuffer(kBlurGauss, "ShadowDstMask",    m.ShadowDstMask);
        ppShader.SetBuffer(kBlurGauss, "RimLightMask",     m.RimLightMask);

        // --- BlurShadowsBox ---
        ppShader.SetBuffer(kBlurBox, "ShadowMask_dbA",   m.ShadowMask_dbA);
        ppShader.SetBuffer(kBlurBox, "ShadowMask_dbB",   m.ShadowMask_dbB);
        ppShader.SetBuffer(kBlurBox, "SharpShadowMask",  m.SharpShadowMask);
        ppShader.SetBuffer(kBlurBox, "ShadowDstMask",    m.ShadowDstMask);
        ppShader.SetBuffer(kBlurBox, "RimLightMask",     m.RimLightMask);

        // --- ApplySharpShadows ---
        ppShader.SetBuffer(kApplySharp, "ShadowMask_dbA",    m.ShadowMask_dbA);
        ppShader.SetBuffer(kApplySharp, "ShadowMask_dbB",    m.ShadowMask_dbB);
        ppShader.SetBuffer(kApplySharp, "SharpShadowMask",   m.SharpShadowMask);
        ppShader.SetBuffer(kApplySharp, "ShadowDstMask",     m.ShadowDstMask);
        ppShader.SetBuffer(kApplySharp, "RimLightMask",      m.RimLightMask);
        ppShader.SetBuffer(kApplySharp, "ShadowSrcFullRes",  m.ShadowSrcFullRes);

        // --- ApplyBlurredShadows ---
        ppShader.SetBuffer(kApplyBlur, "ShadowMask_dbA",     m.ShadowMask_dbA);
        ppShader.SetBuffer(kApplyBlur, "ShadowMask_dbB",     m.ShadowMask_dbB);
        ppShader.SetBuffer(kApplyBlur, "SharpShadowMask",    m.SharpShadowMask);
        ppShader.SetBuffer(kApplyBlur, "ShadowDstMask",      m.ShadowDstMask);
        ppShader.SetBuffer(kApplyBlur, "RimLightMask",       m.RimLightMask);
        ppShader.SetBuffer(kApplyBlur, "ShadowSrcFullRes",   m.ShadowSrcFullRes);
    }

    public void SetPostProcessorTextures(ComputeShader ppShader)
    {
        int kApplySharp = SafeFindKernel(ppShader, "ApplySharpShadows");
        int kApplyBlur  = SafeFindKernel(ppShader, "ApplyBlurredShadows");
        int kApplyNo    = SafeFindKernel(ppShader, "ApplyWithoutShadows");

        if (kApplySharp >= 0)
        {
            ppShader.SetTexture(kApplySharp, "Result", m.renderTexture);
            ppShader.SetTexture(kApplySharp, "PPResult", m.ppRenderTexture);
        }

        if (kApplyBlur >= 0)
        {
            ppShader.SetTexture(kApplyBlur, "Result", m.renderTexture);
            ppShader.SetTexture(kApplyBlur, "PPResult", m.ppRenderTexture);
        }

        if (kApplyNo >= 0)
        {
            ppShader.SetTexture(kApplyNo, "Result", m.renderTexture);
            ppShader.SetTexture(kApplyNo, "PPResult", m.ppRenderTexture);
        }
    }

    public void SetSortShaderBuffers(ComputeShader sortShader)
    {
        sortShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        sortShader.SetBuffer(0, "PTypes", m.PTypeBuffer);

        sortShader.SetBuffer(1, "SpatialLookup", m.SpatialLookupBuffer);

        sortShader.SetBuffer(1, "PDatas", m.PDataBuffer);
        sortShader.SetBuffer(1, "PTypes", m.PTypeBuffer);

        sortShader.SetBuffer(2, "StartIndices", m.StartIndicesBuffer);

        sortShader.SetBuffer(3, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(3, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(3, "PTypes", m.PTypeBuffer);
        sortShader.SetBuffer(3, "PDatas", m.PDataBuffer);

        sortShader.SetBuffer(4, "SpatialLookup", m.SpatialLookupBuffer);
        sortShader.SetBuffer(4, "StartIndices", m.StartIndicesBuffer);
        sortShader.SetBuffer(4, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(5, "SpringCapacities", m.SpringCapacitiesBuffer);

        sortShader.SetBuffer(6, "SpringCapacities", m.SpringCapacitiesBuffer);
        sortShader.SetBuffer(6, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(6, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(6, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);

        sortShader.SetBuffer(7, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(7, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(7, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);

        sortShader.SetBuffer(8, "SpringStartIndices_dbA", m.SpringStartIndicesBuffer_dbA);
        sortShader.SetBuffer(8, "SpringStartIndices_dbB", m.SpringStartIndicesBuffer_dbB);
        sortShader.SetBuffer(8, "SpringStartIndices_dbC", m.SpringStartIndicesBuffer_dbC);
    }

    public void UpdatePSimShaderVariables(ComputeShader pSimShader)
    {
        pSimShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        pSimShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        pSimShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        pSimShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        pSimShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        pSimShader.SetInt("ParticlesNum", m.ParticlesNum);
        pSimShader.SetInt("PTypesNum", m.PTypesNum);
        pSimShader.SetInt("ParticleSpringsCombinedHalfLength", m.ParticleSpringsCombinedHalfLength);
        pSimShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        pSimShader.SetInt("SubTimeStepsPerFrame", m.SubTimeStepsPerFrame);
        pSimShader.SetFloat("LookAheadTime", m.LookAheadTime);
        pSimShader.SetFloat("StateThresholdPadding", m.StateThresholdPadding);
        pSimShader.SetFloat("FluidPadding", Mathf.Max(m.FluidPadding, 0.1f));
        pSimShader.SetFloat("MaxInteractionRadius", m.MaxInteractionRadius);
        pSimShader.SetFloat("MaxPVel", m.MaxPVel);
        pSimShader.SetFloat("InteractionAttractionPower", m.InteractionAttractionPower);
        pSimShader.SetFloat("InteractionRepulsionPower", m.InteractionRepulsionPower);
        pSimShader.SetFloat("InteractionFountainPower", m.InteractionFountainPower);
        pSimShader.SetFloat("InteractionTemperaturePower", m.InteractionTemperaturePower);
        pSimShader.SetFloat("InteractionDampening", m.InteractionDampening);
    }

    public void UpdateRenderShaderVariables(ComputeShader renderShader)
    {
        renderShader.SetFloat("LiquidMetaballsThreshold", m.LiquidMetaballsThreshold);
        renderShader.SetFloat("LiquidMetaballsEdgeDensityWidth", m.LiquidMetaballsEdgeDensityWidth);
        renderShader.SetFloat("VisualLiquidParticleRadius", m.VisualLiquidParticleRadius);
        renderShader.SetFloat("LiquidEdgeWidth", m.LiquidEdgeWidth);
        renderShader.SetFloat("InvLiquidVelocityGradientMaxValue", 1 / m.LiquidVelocityGradientMaxValue);

        renderShader.SetFloat("GasMetaballsThreshold", m.GasMetaballsThreshold);
        renderShader.SetFloat("GasMetaballsEdgeDensityWidth", m.GasMetaballsEdgeDensityWidth);
        renderShader.SetFloat("VisualGasParticleRadius", m.VisualGasParticleRadius);
        renderShader.SetFloat("GasEdgeWidth", m.GasEdgeWidth);
        renderShader.SetFloat("InvGasVelocityGradientMaxValue", 1 / m.GasVelocityGradientMaxValue);
        renderShader.SetFloat("GasNoiseStrength", m.GasNoiseStrength);
        renderShader.SetFloat("GasNoiseDensityDarkeningFactor", m.GasNoiseDensityDarkeningFactor);
        renderShader.SetFloat("GasNoiseDensityOpacityFactor", m.GasNoiseDensityOpacityFactor);

        renderShader.SetFloat("BackgroundUpScaleFactor", m.BackgroundUpScaleFactor);
        renderShader.SetVector("BackgroundBrightness", Utils.Float3ToVector3(m.BackgroundBrightness));
        renderShader.SetBool("MirrorRepeatBackgroundUV", m.MirrorRepeatBackgroundUV);

        renderShader.SetFloat("RBEdgeWidth", m.RBEdgeWidth);
        renderShader.SetFloat("FluidSensorEdgeWidth", m.FluidSensorEdgeWidth);
        renderShader.SetFloat("SensorAreaAnimationSpeed", m.SensorAreaAnimationSpeed);

        renderShader.SetFloat("RBShadowStrength", m.RBShadowStrength);
        renderShader.SetFloat("LiquidShadowStrength", m.LiquidShadowStrength);
        renderShader.SetFloat("GasShadowStrength", m.GasShadowStrength);

        renderShader.SetInt("SpringRenderNumPeriods", m.SpringRenderNumPeriods);
        renderShader.SetFloat("SpringRenderWidth", m.SpringRenderWidth);
        renderShader.SetFloat("SpringRenderHalfMatWidth", m.SpringRenderMatWidth / 2.0f);
        renderShader.SetFloat("SpringRenderRodLength", Mathf.Max(m.SpringRenderRodLength, 0.01f));
        renderShader.SetFloat("TaperThresoldNormalised", m.TaperThresoldNormalised);
        renderShader.SetVector("SpringTextureUVFactor", Utils.Float2ToVector2(m.SpringTextureUVFactor));

        renderShader.SetVector("Resolution", PM.Instance.Resolution);
        renderShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        renderShader.SetVector("ViewScale", PM.Instance.ViewScale);
        renderShader.SetVector("ViewOffset", PM.Instance.ViewOffset);

        renderShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        renderShader.SetFloat("InvMaxInfluenceRadius", m.InvMaxInfluenceRadius);
        renderShader.SetInt("MaxInfluenceRadiusSqr", m.MaxInfluenceRadiusSqr);
        renderShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        renderShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        renderShader.SetInt("ParticlesNum", m.ParticlesNum);
        renderShader.SetInt("PTypesNum", m.PTypesNum);
        renderShader.SetInt("NumRigidBodies", m.NumRigidBodies);
        renderShader.SetInt("NumFluidSensors", m.NumFluidSensors);
        renderShader.SetInt("NumMaterials", m.MaterialsCount);
        renderShader.SetVector("PrecomputedCausticsDims", Utils.Int3ToVector3(m.PrecomputedCausticsDims));
        renderShader.SetFloat("PrecomputedCausticsScaleFactor", m.PrecomputedCausticsScaleFactor);
        renderShader.SetFloat("DynamicCausticsScaleFactor", m.DynamicCausticsScaleFactor);
        renderShader.SetFloat("PrecomputedCausticsZBlurFactor", m.PrecomputedCausticsZBlurFactor);

        renderShader.SetVector("GlobalBrightness", Utils.Float3ToVector3(m.GlobalBrightness));
        renderShader.SetFloat("Contrast", m.Contrast);
        renderShader.SetFloat("Saturation", m.Saturation);
        renderShader.SetFloat("Gamma", m.Gamma);

        // --- New: runtime sun light + RB world UV scale ---
        renderShader.SetVector("sunDir", new Vector2(m.SunDirection.x, m.SunDirection.y));
    }

    public void SetPostProcessorVariables(ComputeShader ppShader)
    {
        ppShader.SetFloat("ShadowDarkness", m.ShadowDarkness);
        ppShader.SetFloat("ShadowFalloff", m.ShadowFalloff);

        // Full-resolution output (Result/PPResult) resolution
        ppShader.SetVector("Resolution", PM.Instance.Resolution);
        ppShader.SetVector("ShadowDirection", new(-Mathf.Cos(Mathf.Deg2Rad * m.ShadowDirection), -Mathf.Sin(Mathf.Deg2Rad * m.ShadowDirection)));

        ppShader.SetInt("ShadowBlurRadius", Mathf.Max(0, m.ShadowBlurRadius));
        ppShader.SetFloat("ShadowDiffusion", Mathf.Max(0f, m.ShadowDiffusion));

        ppShader.SetFloat("RimShadingStrength", m.RimShadingStrength);
        ppShader.SetFloat("RimShadingBleed", m.RimShadingBleed);
        ppShader.SetFloat("RimShadingOpaqueBleed", m.RimShadingOpaqueBleed);

        // --- New: low-resolution shadow grid parameters ---
        int factor = 1 << Mathf.Clamp(m.ShadowDownSampling, 0, 30);
        int w = Mathf.Max(1, m.renderTexture.width  / factor);
        int h = Mathf.Max(1, m.renderTexture.height / factor);
        ppShader.SetVector("ShadowResolution", new Vector2(w, h));
        ppShader.SetInt("ShadowDownsampleFactor", factor);
    }

    public void UpdateSortShaderVariables(ComputeShader sortShader)
    {
        sortShader.SetInt("MaxInfluenceRadius", m.MaxInfluenceRadius);
        sortShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        sortShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        sortShader.SetInt("ParticlesNum", m.ParticlesNum);
        sortShader.SetInt("ParticlesNum_NextPow2", m.ParticlesNum_NextPow2);
    }

    // --- Ner RB shader ---

    public void SetRBSimShaderBuffers(ComputeShader rbSimShader)
    {
        rbSimShader.SetBuffer(0, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(0, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(0, "RecordedFluidDatas", m.RecordedFluidDataBuffer);

        rbSimShader.SetBuffer(0, "SpatialLookup", m.SpatialLookupBuffer);
        rbSimShader.SetBuffer(0, "PTypes", m.PTypeBuffer);
        rbSimShader.SetBuffer(0, "PDatas", m.PDataBuffer);
        rbSimShader.SetBuffer(0, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(1, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(1, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(1, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(2, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(2, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(3, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(3, "RBAdjustments", m.RBAdjustmentBuffer);

        rbSimShader.SetBuffer(4, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(4, "RBVectors", m.RBVectorBuffer);
        
        rbSimShader.SetBuffer(5, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(5, "RBVectors", m.RBVectorBuffer);
        rbSimShader.SetBuffer(5, "RBAdjustments", m.RBAdjustmentBuffer);
        rbSimShader.SetBuffer(5, "RecordedFluidDatas", m.RecordedFluidDataBuffer);

        rbSimShader.SetBuffer(6, "RigidBodies", m.RBDataBuffer);
        rbSimShader.SetBuffer(6, "RBVectors", m.RBVectorBuffer);
    }

    public void UpdateRBSimShaderVariables(ComputeShader rbSimShader)
    {
        rbSimShader.SetVector("BoundaryDims", Utils.Int2ToVector2(m.BoundaryDims));
        rbSimShader.SetFloat("RigidBodyPadding", m.RigidBodyPadding);
        rbSimShader.SetFloat("BoundaryElasticity", m.BoundaryElasticity);
        rbSimShader.SetFloat("BoundaryFriction", m.BoundaryFriction);

        rbSimShader.SetInt("NumRigidBodies", m.NumRigidBodies);
        rbSimShader.SetInt("NumVectors", m.NumRigidBodyVectors);
        rbSimShader.SetInt("NumParticles", m.ParticlesNum);
        rbSimShader.SetVector("ChunksNum", Utils.Int2ToVector2(m.ChunksNum));
        rbSimShader.SetInt("ChunksNumAll", m.ChunksNumAll);
        rbSimShader.SetInt("PTypesNum", m.PTypesNum);

        rbSimShader.SetBool("AllowLinkedRBCollisions", m.AllowLinkedRBCollisions);
        rbSimShader.SetFloat("RB_RBCollisionCorrectionFactor", m.RB_RBCollisionCorrectionFactor);
        rbSimShader.SetFloat("RB_RBFixedCollisionCorrection", m.RB_RBFixedCollisionCorrection);
        rbSimShader.SetFloat("RB_RBRigidConstraintCorrectionFactor", m.RB_RBRigidConstraintCorrectionFactor);

        rbSimShader.SetFloat("MaxRBRotVel", m.MaxRBRotVel);
        rbSimShader.SetFloat("MaxRBVel", m.MaxRBVel);
        rbSimShader.SetFloat("MinRBVelForMovement", m.MinRBVelForMovement);

        rbSimShader.SetFloat("RB_MaxInteractionRadius", m.RB_MaxInteractionRadius);
        rbSimShader.SetFloat("RB_InteractionAttractionPower", m.RB_InteractionAttractionPower);
        rbSimShader.SetFloat("RB_InteractionRepulsionPower", m.RB_InteractionRepulsionPower);
        rbSimShader.SetFloat("RB_InteractionDampening", m.RB_InteractionDampening);
    }

    // -----------------------
    // Helpers
    // -----------------------
    private int SafeFindKernel(ComputeShader shader, string name)
    {
        try { return shader.FindKernel(name); }
        catch { return -1; }
    }
}#pragma kernel RenderBackground; // 0 
#pragma kernel RenderFluids;     // 1
#pragma kernel RenderRigidBodies;// 2
#pragma kernel RenderRigidBodySprings; // 3
#pragma kernel RenderUI; // 4

#pragma multi_compile _ DRAW_RB_CENTROIDS
#pragma multi_compile _ DRAW_FLUID_OUTLINES
#pragma multi_compile _ DISPLAY_FLUID_VELOCITIES
#pragma multi_compile _ USE_CAUSTICS
#pragma multi_compile _ USE_DYNAMIC_CAUSTICS
#pragma multi_compile _ DRAW_UNOCCUPIED_FLUID_SENSOR_AREA
#pragma multi_compile _ DRAW_RB_OUTLINES
#pragma multi_compile _ USE_METABALLS
#pragma multi_compile _ USE_BILINEAR_SAMPLER
#pragma multi_compile _ DO_USE_FAST_COMPILATION

#include "./Helpers/MathResources.hlsl"
#include "./Helpers/DataTypes.hlsl"
#include "./Helpers/Constants.hlsl"

// Liquid rendering settings
const float LiquidMetaballsThreshold;
const float LiquidMetaballsEdgeDensityWidth;
const float VisualLiquidParticleRadius;
const float LiquidEdgeWidth;
const float InvLiquidVelocityGradientMaxValue;

// Gas rendering settings
const float GasMetaballsThreshold;
const float GasMetaballsEdgeDensityWidth;
const float VisualGasParticleRadius;
const float GasEdgeWidth;
const float InvGasVelocityGradientMaxValue;
const float GasNoiseStrength;
const float GasNoiseDensityDarkeningFactor;
const float GasNoiseDensityOpacityFactor;

// Background
const float BackgroundUpScaleFactor;
const float3 BackgroundBrightness;
const bool MirrorRepeatBackgroundUV;

// Other
const float RBEdgeWidth;
const float FluidSensorEdgeWidth;
const float SensorAreaAnimationSpeed;

// ShadowMask strengths
const float RBShadowStrength;
const float LiquidShadowStrength;
const float GasShadowStrength;

// Rigid body springs
const int   SpringRenderNumPeriods;
const float SpringRenderWidth;
const float SpringRenderHalfMatWidth;
const float SpringRenderRodLength;
const float TaperThresoldNormalised;
const float2 SpringTextureUVFactor;

// Other
const uint2 Resolution;
const uint2 BoundaryDims;
const float2 ScreenToViewFactor;
const float2 ViewScale;
const float2 ViewOffset;
const float  InvMaxInfluenceRadius;
const int    MaxInfluenceRadius;
const int    MaxInfluenceRadiusSqr;
const int    NumMaterials;
const int2   ChunksNum;
const uint   ChunksNumAll;
const int    ParticlesNum;
const uint   PTypesNum;
const uint   NumRigidBodies;
const uint   NumFluidSensors;
const int3   PrecomputedCausticsDims;
const float  PrecomputedCausticsScaleFactor;
const float  DynamicCausticsScaleFactor;
const float  PrecomputedCausticsZBlurFactor;

// Global rendering settings
const float3 GlobalBrightness;
const float  GlobalBrightnessFactor;
const float  Contrast;
const float  Saturation;
const float  Gamma;

// Per-timestep-set variables
const float TotalScaledTimeElapsed;
const int   LastTimeSetRand;
const int   NextTimeSetRand;
const float TimeSetLerpFactor;
const int   PrecomputedCausticsZ;

// Outputs
RWTexture2D<unorm float4> Result;

// IMPORTANT: This is the FULL-RES source shadow buffer now.
// The PP shader will downsample this into its low-res working grid.
RWStructuredBuffer<float> ShadowMask;

Texture2D<unorm float4> DynamicCaustics;
Texture2DArray<unorm float> PrecomputedCaustics;

Texture2D<unorm float4> LiquidVelocityGradient;
Texture2D<unorm float4> GasVelocityGradient;
Texture2D<unorm float4> UITexture;
Texture2D<unorm float4> Background;
Texture2D<unorm float4> Atlas;

StructuredBuffer<int2> SpatialLookup; 
StructuredBuffer<int>  StartIndices;

StructuredBuffer<PData>  PDatas;
StructuredBuffer<PType>  PTypes;

StructuredBuffer<RigidBody> RigidBodies;
StructuredBuffer<RBVector>  RBVectors;

StructuredBuffer<SensorArea> SensorAreas;

StructuredBuffer<Mat> Materials;

int Extract_PType(int LastChunkKey_PType_POrder)
{
    return ((uint)LastChunkKey_PType_POrder % (ChunksNumAll * PTypesNum)) / ChunksNumAll;
}

float2 GetTexDims(Texture2D<unorm float4> tex)
{
    float t;
    float2 texDims;
    tex.GetDimensions(0, texDims.x, texDims.y, t);
    return texDims;
}

float3 SampleTextureBilinear(float2 uv, uint2 texLoc, uint2 texDims, Texture2D<unorm float4> tex)
{
    float2 pixelPos = uv * texDims;
 
    int2 texelCoord00 = ((int2)floor(pixelPos)) % texDims;
    int2 texelCoord10 = (texelCoord00 + int2(1, 0)) % texDims;
    int2 texelCoord01 = (texelCoord00 + int2(0, 1)) % texDims;
    int2 texelCoord11 = (texelCoord00 + int2(1, 1)) % texDims;
 
    float3 c00 = tex.Load(int3(texelCoord00, 0)).rgb;
    float3 c10 = tex.Load(int3(texelCoord10, 0)).rgb;
    float3 c01 = tex.Load(int3(texelCoord01, 0)).rgb;
    float3 c11 = tex.Load(int3(texelCoord11, 0)).rgb;
 
    float2 fraction = frac(pixelPos);
    float3 c0 = lerp(c00, c10, fraction.x);
    float3 c1 = lerp(c01, c11, fraction.x);
    float3 sampleCol = lerp(c0, c1, fraction.y);
 
    return sampleCol;
}
 
float3 SampleTexturePoint(float2 uv, uint2 texLoc, uint2 texDims, Texture2D<unorm float4> tex)
{
    uint2 texelCoord = texLoc + ((floor(uv * float2(texDims.x, texDims.y))) % texDims);
    float3 sampleCol = tex.Load(int3(texelCoord, 0)).rgb;
    return sampleCol;
}

float3 SampleTexture(float2 uv, uint2 texLoc, uint2 texDims, Texture2D<unorm float4> tex)
{
    uv = frac(uv);
    
    #if USE_BILINEAR_SAMPLER
        return SampleTextureBilinear(uv, texLoc, texDims, tex);
    #else
        return SampleTexturePoint(uv, texLoc, texDims, tex);
    #endif
}

float4 SampleGradient(float u, Texture2D<unorm float4> gradientTex)
{
    int gradientWidth = GetTexDims(gradientTex).x;
    uint x = (int)(u * gradientWidth);
    x = clamp(x, 0, gradientWidth-1);
    float4 sample = gradientTex.Load(int3(x, 0, 0));
    return float4(sample.rgb, sample.a);
}

float3 GetMaterialColor(Mat mat, float2 uv, Texture2D<unorm float4> atlas)
{
    if (mat.colTexLoc.x != -1)
    {
        float3 sampledColor = SampleTexture(uv, mat.colTexLoc, mat.colTexDims, atlas) * mat.sampleColMul;
        return sampledColor + mat.baseCol;
    }
    else return mat.baseCol;
}

void AdjustContrast(inout float3 color)
{
    color = (color - 0.5) * Contrast + 0.5;
}

void AdjustSaturation(inout float3 color)
{
    float grey = dot(color, float3(0.3, 0.59, 0.11));
    color = lerp(float3(grey, grey, grey), color, Saturation);
}

void ApplyGammaCorrection(inout float3 color)
{
    float invGamma = 1.0 / Gamma;
    color.x = pow(max(color.x, 0.0), invGamma);
    color.y = pow(max(color.y, 0.0), invGamma);
    color.z = pow(max(color.z, 0.0), invGamma);
}

void SetResultColor(uint2 id, float3 color)
{
    // Apply rendering adjustments
    AdjustContrast(color);
    AdjustSaturation(color);
    ApplyGammaCorrection(color);

    // Apply brightness factors
    color *= GlobalBrightness * GlobalBrightnessFactor;

    color = saturate(color);
    Result[id] = float4(color, 1.0);
}

bool ValidChunk(int2 chunk)
{
    return chunk.x >= 0 && chunk.x < ChunksNum.x && chunk.y >= 0 && chunk.y < ChunksNum.y;
}

int GetChunkKey(int2 chunk)
{
    return chunk.y * ChunksNum.x + chunk.x;
}

uint GetPixelKey(uint2 threadID)
{
    return threadID.y * Resolution.x + threadID.x;
}

bool IsOutsideResolutionDims(uint2 threadID)
{
    return threadID.x > Resolution.x || threadID.y > Resolution.y;
}

bool IsOutsideSimBounds(float2 pixelPos)
{
    return 0 > pixelPos.x || pixelPos.x > (float)BoundaryDims.x || 0 > pixelPos.y || pixelPos.y > (float)BoundaryDims.y;
}

bool IsPointInsideRB(float2 pos, RigidBody rb)
{
    pos -= rb.pos;

    uint intersections = 0;
    uint startIndex = rb.startIndex;
    uint endIndex = rb.endIndex;
    uint numVertices = endIndex - startIndex + 1;
    uint pathStartIndex  = startIndex;
    
    float2 firstPathVec = RBVectors[startIndex].pos;
    float2 lastVec = firstPathVec;

    for (uint i = 1; i <= numVertices; i++)
    {
        uint vecIndex = (i == numVertices) ? pathStartIndex : (startIndex + i);
        float2 newVec = RBVectors[vecIndex].pos;

        bool newPathFlag = (newVec.x > PATH_FLAG_THRESHOLD);
        if (newPathFlag)
        {
            if (IsPointToTheLeftOfLine(pos, lastVec, firstPathVec)) intersections++;

            newVec.x -= PATH_FLAG_OFFSET;

            firstPathVec = newVec;
            lastVec = newVec;
            pathStartIndex = vecIndex;
        }
        else
        {
            if (IsPointToTheLeftOfLine(pos, lastVec, newVec)) intersections++;

            lastVec = newVec;
        }
    }

    return ((intersections % 2) == 1);
}

bool IsInsideArea(float2 pos, float2 min2, float2 max2)
{
    return pos.x >= min2.x && pos.x <= max2.x && pos.y >= min2.y && pos.y <= max2.y;
}

float DstToRB(float2 pos, RigidBody rb)
{
    pos -= rb.pos;

    float minDstSqr = 1.#INF;

    uint startIndex = rb.startIndex;
    uint endIndex = rb.endIndex;
    uint numVertices = endIndex - startIndex + 1;
    uint pathStartIndex = startIndex;

    float2 firstPathVec = RBVectors[startIndex].pos;
    float2 lastVec = firstPathVec;

    for (uint i = 1; i <= numVertices; i++)
    {
        uint vecIndex = (i == numVertices) ? pathStartIndex : (startIndex + i);
        float2 newVec    = RBVectors[vecIndex].pos;

        bool newPathFlag = (newVec.x > PATH_FLAG_THRESHOLD);
        if (newPathFlag)
        {
            {
                float2 dst = DstToLineSegment(lastVec, firstPathVec, pos);
                float dstSqr = dot2(dst);
                if (dstSqr < minDstSqr)
                    minDstSqr = dstSqr;
            }

            newVec.x -= PATH_FLAG_OFFSET;

            firstPathVec = newVec;
            lastVec = newVec;
            pathStartIndex = vecIndex;
        }
        else
        {
            float2 dst = DstToLineSegment(lastVec, newVec, pos);
            float dstSqr = dot2(dst);
            if (dstSqr < minDstSqr) minDstSqr = dstSqr;

            lastVec = newVec;
        }
    }

    return (minDstSqr == 1.#INF) ? 1.#INF : sqrt(minDstSqr);
}

float MetaballsDensity(float dst, float invRadius)
{
    float dstR = dst * invRadius;
    return (1 - dstR);
}

float NoiseDensity(float dst, float invRadius, int pSeed)
{
    int lastRandSeed = LastTimeSetRand * ParticlesNum + pSeed;
    int nextRandSeed = NextTimeSetRand * ParticlesNum + pSeed;

    float lastNoise = randNormalized(lastRandSeed);
    float nextNoise = randNormalized(nextRandSeed);
    float lerpNoise = lerp(lastNoise, nextNoise, TimeSetLerpFactor);

    float densityFactor = (1.0 + lerpNoise * GasNoiseStrength) / (1.0 + GasNoiseStrength);
    float noiseDensity = dst * invRadius * densityFactor;

    return noiseDensity;
}

float3 SampleCaustics(uint2 threadID, float3 sampleColMul)
{
    float2 referenceCausticsRes = 512;

    float caustics;
#if USE_DYNAMIC_CAUSTICS
    float2 causticsTexDims = GetTexDims(DynamicCaustics);
    uint2 wrappedThreadID = (uint2)(threadID * DynamicCausticsScaleFactor * causticsTexDims / referenceCausticsRes) % causticsTexDims;
    caustics = (float)DynamicCaustics[wrappedThreadID].rgb;
#else
    uint2 wrappedThreadID = (uint2)(threadID * PrecomputedCausticsScaleFactor * PrecomputedCausticsDims.xy / referenceCausticsRes) % PrecomputedCausticsDims.xy;

    uint zDim = PrecomputedCausticsDims.z;
    uint z = PrecomputedCausticsZ;

    float current = PrecomputedCaustics.Load(int4(wrappedThreadID, z % zDim, 0));
    float next = PrecomputedCaustics.Load(int4(wrappedThreadID, (z + 1) % zDim, 0));
    float prev = PrecomputedCaustics.Load(int4(wrappedThreadID, (z - 1 + zDim) % zDim, 0));
        
    float noBlur = current;
    float fullBlur = (current + next + prev) / 3.0;
        
    caustics = lerp(noBlur, fullBlur, PrecomputedCausticsZBlurFactor);
#endif

    float3 color = caustics * sampleColMul;

    return color;
}

float2 MirrorRepeatUV(float2 uv)
{
    float2 intPart;
    float2 fracPart = frac(uv);
    
    float2 isOdd = floor(uv) - floor(uv / 2.0) * 2.0;
    fracPart = lerp(fracPart, 1.0 - fracPart, step(0.5, isOdd));

    return fracPart;
}

float2 GetPixelPos(uint2 pixelID)
{
    float2 p = (float2)pixelID - (float2)Resolution * 0.5;

    float2 a = 1.0 / ScreenToViewFactor;

    p *= a;

    p += (float2)Resolution * 0.5;

    return p * ViewScale + ViewOffset;
}

float2 ComputeBackgroundUVFromPixelPos(float2 pixelPos)
{
    float2 pixelID = (pixelPos - ViewOffset) / ViewScale - 0.5;

    float2 texDims = GetTexDims(Background);
    float scaleX = (float)Resolution.x / texDims.x;
    float scaleY = (float)Resolution.y / texDims.y;
    float scale = max(scaleX, scaleY);
    float2 scaledTexDims = texDims * scale;

    float2 offset = (scaledTexDims - (float2)Resolution) * 0.5;

    float2 uv = ((pixelID + offset) * BackgroundUpScaleFactor) / scaledTexDims;
    uv = MirrorRepeatBackgroundUV ? MirrorRepeatUV(uv) : frac(uv);

    return uv;
}

[numthreads(TN_R, TN_R, 1)]
void RenderBackground(uint3 id : SV_DispatchThreadID)
{
    if (IsOutsideResolutionDims(id.xy)) return;
    
    float2 pixelPos = GetPixelPos(id.xy);
    float2 uv = ComputeBackgroundUVFromPixelPos(pixelPos);

    float3 sampleCol = SampleTexture(uv, 0, GetTexDims(Background), Background) * BackgroundBrightness;
    SetResultColor(id.xy, sampleCol);

    uint shadowIndex = id.y * Resolution.x + id.x;
    ShadowMask[shadowIndex] = 0.0;
}

float3 BlendWithBackground(float3 color, float opacity, uint2 threadID)
{
    float2 pixelPos = GetPixelPos(threadID);
    float2 uv = ComputeBackgroundUVFromPixelPos(pixelPos);

    float transparency = 1 - opacity;
    return transparency > 0 ? color * opacity + transparency * SampleTexture(uv, 0, GetTexDims(Background), Background) * BackgroundBrightness : color;
}

float DstToBorder(float2 pos, float2 min2, float2 max2)
{
    float dstToLeft = abs(pos.x - min2.x);
    float dstToRight = abs(pos.x - max2.x);
    float dstToBottom = abs(pos.y - min2.y);
    float dstToTop = abs(pos.y - max2.y);

    return min(min(dstToLeft, dstToRight), min(dstToBottom, dstToTop));
}

[numthreads(TN_R,TN_R,1)]
void RenderFluids(uint3 id : SV_DispatchThreadID)
{
    if (IsOutsideResolutionDims(id.xy)) return;

    float2 pixelPos = GetPixelPos(id.xy);
    if (IsOutsideSimBounds(pixelPos))
    {
        SetResultColor(id.xy, 0.1);
        return;
    }

    int2 chunk = (int2)(pixelPos * InvMaxInfluenceRadius);

    int nearestGasLastChunkKey_PType_POrder = INT_MAX;
    int nearestNonGasLastChunkKey_PType_POrder = INT_MAX;
    bool nearestIsNotGas = false;
    float totDensity = 0;
    float totNoiseDensity = 0;
    bool doDrawFluid = false;
    bool drawOutline = false;
    float minDstSqr = 1.#INF;
#if !USE_METABALLS
    float minLiquidRadius = min(MaxInfluenceRadius, VisualLiquidParticleRadius);
    float minLiquidRadiusSqr = minLiquidRadius * minLiquidRadius;
    float minGasRadius = min(MaxInfluenceRadius, VisualGasParticleRadius);
    float minGasRadiusSqr = minGasRadius * minGasRadius;
#endif
#if DISPLAY_FLUID_VELOCITIES
    float totVelocitiesSummative = 0;
    int numParticles = 0;
#endif
    for (int x = -1; x <= 1; x++)
    {
        for (int y = -1; y <= 1; y++)
        {
            int2 curChunk = chunk + int2(x, y);

            if (!ValidChunk(curChunk)) continue;

            int chunkKey = GetChunkKey(curChunk);
            int startIndex = StartIndices[chunkKey];
            
            int iterationCount = 0;
            int index = startIndex;
            while (index < ParticlesNum && iterationCount++ < MAX_SPATIAL_LOOKUP_ITERATIONS)
            {
                if (chunkKey != SpatialLookup[index].y) break;

                int pIndex = SpatialLookup[index].x;
                PData pData = PDatas[pIndex];

                float dstSqr = dot2(pixelPos - pData.pos);
                int lastChunkKey_PType_POrder = pData.lastChunkKey_PType_POrder;
                int pTypeIndex = Extract_PType(lastChunkKey_PType_POrder);
                bool isGas = ((uint)pTypeIndex % 3) == 2;

#if USE_METABALLS
                if (dstSqr < MaxInfluenceRadiusSqr)
                {
                    if (dstSqr < minDstSqr)
                    {
                        minDstSqr = dstSqr;
                        if (isGas) nearestGasLastChunkKey_PType_POrder = lastChunkKey_PType_POrder;
                        else nearestNonGasLastChunkKey_PType_POrder = lastChunkKey_PType_POrder;
                    }

#if DISPLAY_FLUID_VELOCITIES
                    totVelocitiesSummative += length(pData.vel);
                    numParticles++;
#endif

                    float dst = sqrt(dstSqr);
                    totDensity += MetaballsDensity(dst, InvMaxInfluenceRadius);
                    totNoiseDensity += NoiseDensity(dst, InvMaxInfluenceRadius, pIndex);

                    if (!isGas) nearestIsNotGas = true;

#if DRAW_FLUID_OUTLINES
                    if (totDensity > (isGas ? (GasMetaballsThreshold - GasMetaballsEdgeDensityWidth) : (LiquidMetaballsThreshold - LiquidMetaballsEdgeDensityWidth)))
                    {
                        drawOutline = true;
                        doDrawFluid = true;
                    }
#endif

                    if (totDensity > (isGas ? GasMetaballsThreshold : LiquidMetaballsThreshold))
                    {
#if DRAW_FLUID_OUTLINES
                        drawOutline = false;
#else
                        doDrawFluid = true;
#endif
                    }
                }
#else
                if (dstSqr < (isGas ? min(minGasRadius, minDstSqr) : min(minLiquidRadiusSqr, minDstSqr)))
                {
#if DISPLAY_FLUID_VELOCITIES
                    totVelocitiesSummative += length(pData.vel);
                    numParticles++;
#endif

                    doDrawFluid = true;
                    minDstSqr = dstSqr;
                    if (isGas) nearestGasLastChunkKey_PType_POrder = lastChunkKey_PType_POrder;
                    else nearestNonGasLastChunkKey_PType_POrder = lastChunkKey_PType_POrder;

                    if (!isGas) nearestIsNotGas = true;

#if DRAW_FLUID_OUTLINES
                    if (sqrt(dstSqr) > (isGas ? (minGasRadius - GasEdgeWidth) : (minLiquidRadius - LiquidEdgeWidth))) drawOutline = true;
                    else drawOutline = false;
#endif
                }
#endif

                index++;
            }
        }
    }

    float3 result = 0;
    float4 sensorAreaTint = 0;
    bool isInsideAnySensorArea = false;
    for (uint sensorAreaIndex = 0; sensorAreaIndex < NumFluidSensors; sensorAreaIndex++)
    {
        SensorArea sensorArea = SensorAreas[sensorAreaIndex];
        if (IsInsideArea(pixelPos, sensorArea.min, sensorArea.max))
        {
            isInsideAnySensorArea = true;

            float distanceToBorder = DstToBorder(pixelPos, sensorArea.min, sensorArea.max);
            if (distanceToBorder < FluidSensorEdgeWidth)
            {
                sensorAreaTint += sensorArea.lineColor;
                continue;
            }

            float patternMod = sensorArea.patternMod;
            float patternPos = pixelPos.x + pixelPos.y + TotalScaledTimeElapsed * SensorAreaAnimationSpeed;
            float patternPosMod = patternPos % patternMod;

            if (patternPosMod < patternMod * 0.5) sensorAreaTint += sensorArea.colorTint;
        }
    }

    sensorAreaTint.a = clamp(sensorAreaTint.a, 0.0, 1.0);

    int nearestLastChunkKey_PType_POrder = nearestIsNotGas ? nearestNonGasLastChunkKey_PType_POrder : nearestGasLastChunkKey_PType_POrder;
    if (doDrawFluid)
    {
        int nearestPTypeIndex = Extract_PType(nearestLastChunkKey_PType_POrder);
        Mat mat = Materials[PTypes[nearestPTypeIndex].matIndex];

        if (drawOutline) result = mat.edgeCol;
        else
        {
            float3 color;
            float opacity;
#if DISPLAY_FLUID_VELOCITIES
            float avgVel = totVelocitiesSummative / numParticles;
            float4 gradientCol;
#endif
            if (nearestIsNotGas)
            {
#if USE_CAUSTICS
                color = SampleCaustics(id.xy, mat.sampleColMul) + mat.baseCol;
#else
                color = mat.baseCol;
#endif
                opacity = mat.opacity;

#if DISPLAY_FLUID_VELOCITIES
                gradientCol = SampleGradient(avgVel * InvLiquidVelocityGradientMaxValue, LiquidVelocityGradient);
#endif
            }
            else // isGas
            {
                opacity = mat.opacity * max(1.0 + totDensity * GasNoiseDensityOpacityFactor, 0.0);
                color = mat.baseCol + max(1.0 - totNoiseDensity * GasNoiseDensityDarkeningFactor, 0.0);

#if DISPLAY_FLUID_VELOCITIES
                float gradientFactor = avgVel * InvGasVelocityGradientMaxValue;
                gradientCol = SampleGradient(gradientFactor, GasVelocityGradient);
#endif
            }
            color *= GlobalBrightness;
            result = BlendWithBackground(color, opacity, id.xy);

#if DISPLAY_FLUID_VELOCITIES
            result = lerp(result, gradientCol.rgb, gradientCol.a);
#endif
        }

#if !DRAW_UNOCCUPIED_FLUID_SENSOR_AREA
        if (isInsideAnySensorArea) result = sensorAreaTint.rgb * sensorAreaTint.a + result * (1.0 - sensorAreaTint.a);
        SetResultColor(id.xy, result);
#endif

        uint shadowIndex = id.y * Resolution.x + id.x;
        ShadowMask[shadowIndex] = nearestIsNotGas ? LiquidShadowStrength : GasShadowStrength;
    }

#if DRAW_UNOCCUPIED_FLUID_SENSOR_AREA
    if (!doDrawFluid && isInsideAnySensorArea)
    {
        float3 background = BlendWithBackground(0, 0, id.xy);
        result = sensorAreaTint.rgb * sensorAreaTint.a + background * (1.0 - sensorAreaTint.a);
    }
    else if (!doDrawFluid) result = BlendWithBackground(0, 0, id.xy);

    if (isInsideAnySensorArea) result = sensorAreaTint.rgb * sensorAreaTint.a + result * (1.0 - sensorAreaTint.a);

    SetResultColor(id.xy, result);
#endif
}

[numthreads(TN_R,TN_R,1)]
void RenderRigidBodies(uint3 id : SV_DispatchThreadID)
{
    if (IsOutsideResolutionDims(id.xy)) return;
    
    float2 pixelPos = GetPixelPos(id.xy);

    int   highestRenderPriority = 0;
    bool  rigidBodyFound = false;
    bool  drawOutline    = false;
    int   matIndex = 0;
    float3 transformData = 0;
    float outlineAlpha   = 1.0;
    
    for (uint rbIndex = 0; rbIndex < NumRigidBodies; rbIndex++)
    {
        RigidBody rb = RigidBodies[rbIndex];

        float dstSqr = dot2(pixelPos - rb.pos);

#if DRAW_RB_CENTROIDS
        // (optional centroid drawing omitted)
#endif

        int renderPriority = rb.renderPriority;
        if (renderPriority > highestRenderPriority && dstSqr < abs(rb.maxRadiusSqr))
        {
            if (IsPointInsideRB(pixelPos, rb))
            {
                highestRenderPriority = renderPriority;
                rigidBodyFound = true;
                matIndex = rb.matIndex;
                transformData = float3(rb.pos.x, rb.pos.y, rb.totRot);

#if DRAW_RB_OUTLINES
                float dst = DstToRB(pixelPos, rb);
                float realRBEdgeWidth = RBEdgeWidth;
                float aaRange = RBEdgeWidth;
                float outerEdge = realRBEdgeWidth + aaRange;
                if (dst < outerEdge)
                {
                    drawOutline = true;
                    outlineAlpha = (dst > realRBEdgeWidth)
                        ? (1.0 - saturate((dst - realRBEdgeWidth) / aaRange))
                        : 1.0;
                }
                else
                {
                    drawOutline = false;
                }
#endif
            }
        }
    }

    if (rigidBodyFound)
    {
        Mat mat = Materials[matIndex];

        float2 localPixelPos =
            rotate2D(pixelPos - transformData.xy, -transformData.z);
        bool doMirrorRepeat = (mat.colTexUpScaleFactor > 0);
        float2 localUV =
            abs(mat.colTexUpScaleFactor) * localPixelPos
            / min(BoundaryDims.x, BoundaryDims.y) + mat.sampleOffset;
        localUV = doMirrorRepeat ? MirrorRepeatUV(localUV) : frac(localUV);
        float3 baseColor = GetMaterialColor(mat, localUV, Atlas);

        float3 finalColor = baseColor;
        if (drawOutline)
        {
            float3 edgeCol = mat.edgeCol;
            if (!AreAllComponentsEqualTo(edgeCol, -1))
            {
                finalColor = lerp(baseColor, edgeCol, outlineAlpha);
            }
        }

        float3 result = BlendWithBackground(finalColor, mat.opacity, id.xy);
        SetResultColor(id.xy, result);

        uint shadowIndexRB = id.y * Resolution.x + id.x;
        ShadowMask[shadowIndexRB] = RBShadowStrength;
    }
}

float2 ClosestPointZigZag(float2 localPos, float amplitude, int numPeriods, float startX, float totalLength)
{
    float totalPeriodLength = totalLength / numPeriods;
    float halfPeriod = totalPeriodLength * 0.5;

    float x = localPos.x - startX;

    int periodIndex = int(floor(x / totalPeriodLength));

    float xInPeriod = x - periodIndex * totalPeriodLength;

    float t = x / totalLength;
    float taperFactor = saturate(min(t / TaperThresoldNormalised, (1.0 - t) / TaperThresoldNormalised));

    float taperedAmplitude = amplitude * taperFactor;

    float2 segmentStart, segmentEnd;

    if (xInPeriod < halfPeriod)
    {
        segmentStart = float2(startX + periodIndex * totalPeriodLength, -taperedAmplitude * 0.5);
        segmentEnd = float2(segmentStart.x + halfPeriod, taperedAmplitude * 0.5);
    }
    else
    {
        segmentStart = float2(startX + periodIndex * totalPeriodLength + halfPeriod, taperedAmplitude * 0.5);
        segmentEnd = float2(segmentStart.x + halfPeriod, -taperedAmplitude * 0.5);
    }

    float2 segmentVec = segmentEnd - segmentStart;
    float2 pointVec = localPos - segmentStart;

    float u = dot(pointVec, segmentVec) / dot(segmentVec, segmentVec);
    u = clamp(u, 0.0, 1.0);

    float2 closestPoint = segmentStart + u * segmentVec;
    return closestPoint;
}

bool IsOnSpring(float2 localPos, float springLength, float halfSpringWidth)
{
    if (!(localPos.x >= 0.0 && localPos.x <= springLength && abs(localPos.y) <= halfSpringWidth)) return false;
    
    if ((localPos.x < SpringRenderRodLength || localPos.x > springLength - SpringRenderRodLength) && abs(localPos.y) < SpringRenderHalfMatWidth) return true;

    float midStartX = SpringRenderRodLength;
    float midEndX = springLength - SpringRenderRodLength;

    if (localPos.x >= midStartX && localPos.x <= midEndX)
    {
        float midLength = midEndX - midStartX;

        float amplitude = halfSpringWidth - SpringRenderHalfMatWidth;
        float2 closestPoint = ClosestPointZigZag(localPos, amplitude, SpringRenderNumPeriods, midStartX, midLength);

        if (length(localPos - closestPoint) <= SpringRenderHalfMatWidth) return true;
    }

    return false;
}

void TintRed(inout float3 color, float redTint)
{
    redTint = saturate(redTint);
    color = lerp(color, float3(1.0, 0.0, 0.0), redTint);
}

float2 NormalizeLocalSpringPos(float2 value, float rodLength, float totalLength, float springLength, float halfSpringWidth)
{
    if (value.x < rodLength)
    {
        value.x = value.x / rodLength * (rodLength / totalLength);
    }
    else if (value.x > springLength - rodLength)
    {
        value.x = (value.x - (springLength - rodLength)) / rodLength * ((rodLength / totalLength)) + (1.0 - (rodLength / totalLength));
    }
    else
    {
        float middleLength = springLength - 2.0 * rodLength;
        value.x = (value.x - rodLength) / middleLength * ((totalLength - 2.0 * rodLength) / totalLength) + (rodLength / totalLength);
    }

    value.y = (value.y / halfSpringWidth) * 0.5 + 0.5;

    return value;
}

[numthreads(TN_R,TN_R,1)]
void RenderRigidBodySprings(uint3 id : SV_DispatchThreadID)
{
    if (IsOutsideResolutionDims(id.xy))return;

    float2 pixelPos = GetPixelPos(id.xy);

    bool  springFound = false;
    float2 posNorm    = 0;
    float springForce = 0;
    int   matIndex    = 0;
    int   highestRenderPriority = -1;

#if !DO_USE_FAST_COMPILATION
    // [unroll(MAX_RIGIDBODIES_NUM)] // optional
#endif
    for (uint rbIndex = 0; rbIndex < MAX_RIGIDBODIES_NUM; rbIndex++)
    {
        if (rbIndex >= NumRigidBodies) break;

        RigidBody rbA = RigidBodies[rbIndex];
        if (rbA.springMatIndex == -1) continue;
        
        float stiffness = rbA.springStiffness;

        bool isLinked = rbA.linkedRBIndex != -1 && rbA.linkedRBIndex != (int)rbIndex;
        bool rigidConstraint = stiffness == 0.0;
        int renderPriority = rbA.renderPriority;
        if (!isLinked || rigidConstraint || renderPriority <= highestRenderPriority) continue;

        RigidBody rbB = RigidBodies[rbA.linkedRBIndex];

        float2 worldLinkPosA = rbA.pos + rotate2D(rbA.localLinkPosThisRB, rbA.totRot);
        float2 worldLinkPosB = rbB.pos + rotate2D(rbA.localLinkPosOtherRB, rbB.totRot);
        
        float2 localSpringEnd = worldLinkPosB - worldLinkPosA;
        float springLength = length(localSpringEnd);

        if (springLength == 0.0) continue;

        float theta = atan2(localSpringEnd.y, localSpringEnd.x);

        float2 pixelVec = pixelPos - worldLinkPosA;

        float2 localPixelPos = rotate2D(pixelVec, -theta);

        float halfSpringWidth = SpringRenderWidth * 0.5;

        bool isOnSpring = IsOnSpring(localPixelPos, springLength, halfSpringWidth);
        if (isOnSpring)
        {
            springFound = true;
            highestRenderPriority = renderPriority;
            springForce = rbA.recordedSpringForce;
            matIndex = rbA.springMatIndex;
            posNorm = NormalizeLocalSpringPos(localPixelPos, SpringRenderRodLength, rbA.springRestLength, springLength, halfSpringWidth);
        }
    }

    if (springFound)
    {
        Mat mat = Materials[matIndex];
        if (mat.opacity <= 0) return;

        float2 uv = MirrorRepeatUV(abs(mat.colTexUpScaleFactor) * posNorm / (float2)BoundaryDims + mat.sampleOffset);
        uv *= SpringTextureUVFactor;

        float3 result = GetMaterialColor(mat, uv, Atlas) + mat.baseCol;

        float redTint = RED_TINT_FACTOR * springForce;
        TintRed(result, redTint);
        
        SetResultColor(id.xy, result);

        uint  shadowIndexRB = id.y * Resolution.x + id.x;
        float prev          = ShadowMask[shadowIndexRB];
        float prevMag       = max(prev, 0.0);
        ShadowMask[shadowIndexRB] = -(prevMag + 1.0);
    }
}

[numthreads(TN_R,TN_R,1)]
void RenderUI(uint3 id : SV_DispatchThreadID)
{
    if (IsOutsideResolutionDims(id.xy))return;

    float4 uiColor = UITexture[id.xy];
    if (uiColor.a != 0) Result[id.xy] = uiColor;
}