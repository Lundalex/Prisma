using System;
using System.Collections.Generic;
using Resources2;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;
using Debug = UnityEngine.Debug;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ProgramManagerAsset", menuName = "ProgramManager")]
public class ProgramManager : ScriptableObject
{
    // References
    public UserSettings userSettings;
    public Material lineMaterial;
    [NonSerialized] public ProgramLifeCycleManager lifeCycleManager;
    [NonSerialized] public Main main;
    [NonSerialized] public NotificationManager2 notificationManager;
    [NonSerialized] public SensorManager sensorManager;
    [NonSerialized] public FluidSpawnerManager fluidSpawnerManager;
    [NonSerialized] public Transform languageSelectDropdown;
    [NonSerialized] public GameObject fullscreenView;

    // Persisted between scene reloads: the last applied scale.
    [Header("Render Scale (Persisted)")]
    [Tooltip("Stored copy of the active ResolutionScale. Main sets it; this keeps it across scene reloads.")]
    public Main.ResolutionScale StoredResolutionScale = Main.ResolutionScale.Scale_1_1;

    // UI elements
    [NonSerialized] public List<SensorData> sensorDatas = new();
    [NonSerialized] public List<RigidBodyArrow> rigidBodyArrows = new();
    [NonSerialized] public List<FluidArrowField> fluidArrowFields = new();
    [NonSerialized] public List<UserUIElement> userUIElements = new();

    // Globally accessed variables
    [NonSerialized] public bool programStarted;
    [NonSerialized] public bool sceneIsResetting;
    [NonSerialized] public bool doOnSettingsChanged;
    [NonSerialized] public float globalBrightnessFactor;
    [NonSerialized] public float timeScale = 1;
    [NonSerialized] public bool isAnySensorSettingsViewActive;
    [NonSerialized] public bool programPaused;
    [NonSerialized] public bool pauseOnStart;
    [NonSerialized] public bool slowMotionActive;
    [NonSerialized] public bool frameStep;
    [NonSerialized] public int frameCount;
    [NonSerialized] public float clampedDeltaTime;
    [NonSerialized] public float scaledDeltaTime;
    [NonSerialized] public float totalTimeElapsed;
    [NonSerialized] public float totalScaledTimeElapsed;
    [NonSerialized] public float totalRLTimeSinceSceneLoad;
    [NonSerialized] public float timeSetRandTimer;
    [NonSerialized] public Vector2 Resolution;
    [NonSerialized] public int2 ResolutionInt2;
    [NonSerialized] public static readonly float MaxDeltaTime = 1 / 30.0f;
    private static readonly float MinTimeScaleForRunningProgram = 0.01f;
    [NonSerialized] public Vector2 ScreenToViewFactorUI;
    [NonSerialized] public Vector2 ScreenToViewFactorScene;
    [NonSerialized] public Vector2 ViewScale;
    [NonSerialized] public Vector2 ViewOffset;
    [NonSerialized] public bool isStandardResolution;
    [NonSerialized] public string lastOpenedScene;

    public event Action<bool> OnProgramUpdate;
    public event Action OnNewLanguageSelected;
    public event Action OnPreStart;
    public event Action<bool> OnSetNewPauseState;
    public event Action<bool> OnSetNewSlowMotionState;

    // Start confirmation timing
    [NonSerialized] public static readonly float msStartConfimationDelay = 650.0f;
    [NonSerialized] public static readonly float msControlsTipDelay = 2000.0f;
    public static StartConfirmationStatus startConfirmationStatus;
    public static bool hasShownStartConfirmation = false;

    // Private - Camera & Render
    private Camera uiCam;
    private Vector2 uiViewMin;
    private Vector2 uiViewDims;
    private bool viewTransformInitiated;
    private float lastScreenRatio;
    private readonly Vector2 StandardResolution = new(1920, 1080);

    // Private - Animated Texture Scrolling
    private static readonly float NonSettingsMaterialScrollSpeed = 2.0f;
    private static readonly float SettingsMaterialScrollSpeed = 1.0f;
    private float offset;

    // Private - Pause UI
    [NonSerialized] private TMP_Text pauseText;
    [NonSerialized] private float pauseTextAlpha = -1f;

    // Key inputs
    private Timer rapidFrameSteppingTimer;
    private static readonly float rapidFrameSteppingDelay = 0.1f;
    private static readonly float SlowMotionFactor = 4.0f;

    [NonSerialized] public static bool hasBeenReset = false;

    // Track last applied scale and default resolution to detect changes
    private Main.ResolutionScale _appliedResolutionScale = Main.ResolutionScale.Scale_1_1;
    private int2 _appliedDefaultResolution = int2.zero;

    // ─────────────────────────────────────────────────────────────
    // NEW: Drag-lock and Hover-arbiter (single owner at a time)
    // ─────────────────────────────────────────────────────────────
    [NonSerialized] private SensorUI _dragOwner;     // only one sensor may drag at a time

    // Singleton
    private static ProgramManager _instance;
    public static ProgramManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = Resources.Load<ProgramManager>("ProgramManagerAsset");
            }
            return _instance;
        }
    }

    public void Initialize()
    {
        SetReferences();
        if (!hasBeenReset) StoredResolutionScale = main.ResolutionScaleSetting;
        Main.ResolutionScale desiredScale =
            hasBeenReset
                ? StoredResolutionScale
                : main.ResolutionScaleSetting;
        
        ApplyResolutionScale(desiredScale, forceSceneReset: false);

        ScreenToViewFactorUI = GetScreenToViewFactor(Resolution.x / Resolution.y);
        ScreenToViewFactorScene = GetScreenToViewFactor(Screen.width / (float)Screen.height);
        main.SetScreenToViewFactor(ScreenToViewFactorScene);
        (ViewScale, ViewOffset) = GetViewTransform();
        SetStaticUIPositions();
    }

    public void Start()
    {
        OnPreStart?.Invoke();

        Initialize();

        main.StartScript();
        fluidSpawnerManager.StartScript(main);
        sensorManager.StartScript(main);
    }

    public void Update()
    {
        // If user edits Main.DefaultResolution or Main.ResolutionScaleSetting at runtime,
        // store & apply, then reset scene.
        if (Application.isPlaying && main != null)
        {
            bool scaleChanged = main.ResolutionScaleSetting != _appliedResolutionScale;
            bool baseChanged = main.DefaultResolution.x != _appliedDefaultResolution.x ||
                            main.DefaultResolution.y != _appliedDefaultResolution.y;

            if (scaleChanged || baseChanged || !hasBeenReset)
            {
                ApplyResolutionScale(main.ResolutionScaleSetting, forceSceneReset: true);
            }
        }

        if (sceneIsResetting) return;
        CheckStartConfirmation();

        sceneIsResetting = CheckInputs();
        if (sceneIsResetting)
        {
            ResetScene();
            return;
        }

        // Per frame "constants"
        clampedDeltaTime = Mathf.Min(Time.deltaTime, MaxDeltaTime);
        scaledDeltaTime = clampedDeltaTime * timeScale * (slowMotionActive ? 1.0f / SlowMotionFactor : 1.0f);
        totalRLTimeSinceSceneLoad += Time.deltaTime;

        // Rendering
        UpdateAnimatedDashedLineOffset(clampedDeltaTime);
        LerpGlobalBrightness(clampedDeltaTime);
        LerpTimeScale(clampedDeltaTime);
        LerpSensorUIScale(clampedDeltaTime);
        UpdatePauseTextAlpha(clampedDeltaTime); // NEW: fade PauseText alpha while pausing/unpausing

        if (doOnSettingsChanged && programStarted)
        {
            #if UNITY_EDITOR
                main.OnSettingsChanged();
            #endif
            lifeCycleManager.SetTargetFrameRate();
            doOnSettingsChanged = false;
        }

        bool simulateThisFrame = !programPaused || frameStep;
        if (programPaused && frameStep)
        {
            StringUtils.LogIfInEditor("Stepped forward 1 frame");
            frameStep = false;
        }

        if (simulateThisFrame && timeScale > MinTimeScaleForRunningProgram)
        {
            // Performance report
            CheckPerformance();

            // Update runtime scripts
            fluidSpawnerManager.UpdateScript();
            main.UpdateScript();

            // Update sensors & arrows
            UpdateSensorScripts();
            UpdateArrowScripts();

            // Time accumulation
            totalTimeElapsed += clampedDeltaTime;
            totalScaledTimeElapsed += scaledDeltaTime;

            // Notify timers/subscribers
            TriggerProgramUpdate(true);

            // Request sensor refresh
            sensorManager.RequestUpdate();
        }
        else
        {
            main.RunGPUSorting();
            main.RunRenderShader();
            UpdateArrowScripts();
            UpdateSensorScripts();
            TriggerProgramUpdate(false);
        }
    }

    public void ResetScene()
    {
        // Clear timer subscribers to prevent leaks
        OnProgramUpdate = null;
        // Dispose timers
        rapidFrameSteppingTimer?.Dispose();
        rapidFrameSteppingTimer = null;

        startConfirmationStatus = StartConfirmationStatus.NotStarted;
        hasBeenReset = true;
        UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void TriggerProgramUpdate(bool doUpdateClampedTime) => OnProgramUpdate?.Invoke(doUpdateClampedTime);

    private void CheckStartConfirmation()
    {
        if (startConfirmationStatus == StartConfirmationStatus.NotStarted)
        {
            programPaused = true;
            return;
        }
        else if (startConfirmationStatus == StartConfirmationStatus.Waiting)
        {
            programPaused = true;
        }
        else if (startConfirmationStatus == StartConfirmationStatus.Complete)
        {
            programPaused = pauseOnStart;
            if (pauseOnStart) TriggerSetPauseState(true);

            startConfirmationStatus = StartConfirmationStatus.None;
        }
    }

    private bool CheckInputs()
    {
        if (!TMPInputChecker.UserIsUsingInputField())
        {
            if (Input.GetKeyDown(KeyCode.R) && CheckAllowRestart())
            {
                Debug.Log("'R' key pressed. Scene resetting...");
                return true;
            }
            else if (Input.GetKeyDown(KeyCode.Space))
            {
                TriggerSetPauseState(!programPaused);
            }
            else if (Input.GetKey(KeyCode.F) && rapidFrameSteppingTimer != null && rapidFrameSteppingTimer.Check())
            {
                if (!programPaused) TriggerSetPauseState(true);
                frameStep = !frameStep;
            }
            else if (Input.GetKeyDown(KeyCode.S))
            {
                if (frameStep || programPaused)
                {
                    if (programPaused) TriggerSetPauseState(false);
                    frameStep = false;
                }
                OnSetNewSlowMotionState?.Invoke(!slowMotionActive);
            }
            else if (Input.GetKeyDown(KeyCode.Escape))
            {
                CloseAllSensorUISettingsPanels();
            }
        }

        // Screen resizing
        float screenRatio = Screen.width / (float)Screen.height;
        if (screenRatio != lastScreenRatio)
        {
            ScreenToViewFactorScene = GetScreenToViewFactor(screenRatio);
            main.SetScreenToViewFactor(ScreenToViewFactorScene);
            lastScreenRatio = screenRatio;
        }

        return false;
    }

    public bool CheckAllowRestart() => startConfirmationStatus == StartConfirmationStatus.Complete || startConfirmationStatus == StartConfirmationStatus.None;

    #region Performance Checking
    public float InterpolatedFPS = 120.0f;
    public float InterpolatedSimSpeed = 2.0f;
    private const float FPSLerpsFactor = 1.0f;
    private const float SimSpeedLerpFactor = 1.0f;

    private void CheckPerformance()
    {
        float currentFPS = 1f / Time.deltaTime;
        float targetFrameTime = Time.deltaTime / main.GetTimeStepsPerFrame();
        float simSpeed = Mathf.Min(targetFrameTime, main.TimeStep) / targetFrameTime;

        InterpolatedFPS = Mathf.Lerp(InterpolatedFPS, currentFPS, FPSLerpsFactor * Time.deltaTime);
        InterpolatedSimSpeed = Mathf.Lerp(InterpolatedSimSpeed, simSpeed, SimSpeedLerpFactor * Time.deltaTime);
    }
    #endregion

    private void UpdateSensorScripts()
    {
        foreach (SensorData sensorData in sensorDatas) sensorData.sensor.UpdateScript();
    }

    private void UpdateArrowScripts()
    {
        foreach (RigidBodyArrow rigidBodyArrow in rigidBodyArrows) rigidBodyArrow.UpdateScript();
        foreach (FluidArrowField fluidArrowField in fluidArrowFields) fluidArrowField.UpdateScript();
    }

    private void CloseAllSensorUISettingsPanels()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            sensorData.sensorUI.settingsViewWindowManager.OpenPanel("DefaultDisplay");
            sensorData.sensorUI.SetSettingsViewAsDisabled();
        }
    }

    private void UpdateAnimatedDashedLineOffset(float deltaTime)
    {
        float speedFactor = isAnySensorSettingsViewActive ? SettingsMaterialScrollSpeed : NonSettingsMaterialScrollSpeed;
        offset += deltaTime * speedFactor;
        lineMaterial.mainTextureOffset = new Vector2(offset, 0);
    }

    private void SetReferences()
    {
        lifeCycleManager = GameObject.FindGameObjectWithTag("LifeCycleManager").GetComponent<ProgramLifeCycleManager>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        notificationManager = GameObject.FindGameObjectWithTag("NotificationManager2").GetComponent<NotificationManager2>();
        fluidSpawnerManager = GameObject.FindGameObjectWithTag("FluidSpawnerManager").GetComponent<FluidSpawnerManager>();
        languageSelectDropdown = GameObject.FindGameObjectWithTag("LanguageSelect")?.GetComponent<Transform>();
        fullscreenView = GameObject.FindGameObjectWithTag("FullscreenView");
    }

    public void ResetData(bool pauseOnStart)
    {
        OnProgramUpdate = null;

        programStarted = true;
        sceneIsResetting = false;
        doOnSettingsChanged = false;
        isAnySensorSettingsViewActive = false;
        programPaused = pauseOnStart;
        this.pauseOnStart = pauseOnStart;
        slowMotionActive = false;

        if (!hasShownStartConfirmation)
        {
            startConfirmationStatus = StartConfirmationStatus.NotStarted;
        }
        else
        {
            startConfirmationStatus = StartConfirmationStatus.None;
        }

        totalTimeElapsed = 0f;
        totalScaledTimeElapsed = 0f;
        totalRLTimeSinceSceneLoad = 0f;
        frameCount = 0;
        globalBrightnessFactor = -1f;

        sensorDatas = new();
        rigidBodyArrows = new();
        fluidArrowFields = new();
        userUIElements = new();

        rapidFrameSteppingTimer?.Dispose();
        rapidFrameSteppingTimer = new Timer(rapidFrameSteppingDelay, TimeType.NonClamped, true, rapidFrameSteppingDelay);

        InterpolatedFPS = 120.0f;
        InterpolatedSimSpeed = 2.0f;

        // Clear interaction owners on reset
        _dragOwner = null;
    }

    public void AddSensor(SensorUI sensorUI, Sensor sensor)
    {
        int sensorIndex = sensorDatas.Count;
        sensorUI.sensorIndex = sensorIndex;
        sensorDatas.Add(new SensorData(sensor, sensorUI, sensorUI.gameObject, false));

        sensorUI.OnSettingsViewStatusChanged += (isActive) => SetSensorSettingsViewStatus(sensorIndex, isActive);
        sensorUI.OnIsBeingDragged += () => MoveSensorToFront(sensorIndex);
    }

    public void AddRigidBodyArrow(RigidBodyArrow rigidBodyArrow)
    {
        rigidBodyArrows.Add(rigidBodyArrow);
    }

    public void AddFluidArrowField(FluidArrowField fluidArrowField)
    {
        fluidArrowFields.Add(fluidArrowField);
    }

    public void AddUserInput(UserUIElement userUIElement) => userUIElements.Add(userUIElement);

    private void SetSensorSettingsViewStatus(int sensorIndex, bool isSettingsViewActive)
    {
        sensorDatas[sensorIndex].isSettingsViewActive = isSettingsViewActive;

        if (isSettingsViewActive) MoveSensorToFront(sensorIndex);

        isAnySensorSettingsViewActive = CheckAnySensorSettingsViewActive();
    }

    private void MoveSensorToFront(int sensorIndex)
    {
        int frontIndex = sensorDatas.Count;
        sensorDatas[sensorIndex].sensorUIObject.transform.SetSiblingIndex(frontIndex);
    }

    private bool CheckAnySensorSettingsViewActive()
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.isSettingsViewActive) return true;
        }
        return false;
    }

    public bool CheckAnyUIElementHovered()
    {
        if (startConfirmationStatus == StartConfirmationStatus.Waiting || startConfirmationStatus == StartConfirmationStatus.NotStarted) return false;

        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.sensorUI.isPointerHovering) return true;
        }
        foreach (UserUIElement userUIElement in userUIElements)
        {
            if (userUIElement.pointerHoverArea == null) continue;
            if (userUIElement.pointerHoverArea.CheckIfHovering()) return true;
        }
        return false;
    }

    public bool CheckAnySensorBeingMoved()
    {
        if (startConfirmationStatus == StartConfirmationStatus.Waiting || startConfirmationStatus == StartConfirmationStatus.NotStarted) return false;

        foreach (SensorData sensorData in sensorDatas)
        {
            if (sensorData.sensorUI.isBeingMoved) return true;
        }
        return false;
    }

    public bool CheckAnySensorBeingMoved(SensorUI sensorUIException = null)
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            SensorUI sensorUI = sensorData.sensorUI;

            if (sensorUIException != null)
            {
                if (sensorUI == sensorUIException) continue;
            }

            if (sensorUI.isBeingMoved) return true;
        }
        return false;
    }

    // ─────────────────────────────────────────────────────────────
    // Single-drag lock API
    // ─────────────────────────────────────────────────────────────
    public bool TryBeginSensorDrag(SensorUI requester)
    {
        if (_dragOwner == null || _dragOwner == requester)
        {
            _dragOwner = requester;
            return true;
        }
        return false;
    }

    public void EndSensorDrag(SensorUI requester)
    {
        if (_dragOwner == requester)
            _dragOwner = null;
    }

    public bool IsAnotherSensorDragging(SensorUI requester = null)
    {
        return _dragOwner != null && _dragOwner != requester;
    }

    // ─────────────────────────────────────────────────────────────
    // Single-hover arbiter: only the top-most hovered sensor may react
    // (Recomputed fresh on every query to avoid order/caching issues)
    // When dragging, only the drag owner may react to hover.
    // ─────────────────────────────────────────────────────────────
    public bool HoverMayReact(SensorUI candidate)
    {
        // If we're dragging a different sensor, nobody else may react to hover.
        if (_dragOwner != null && _dragOwner != candidate)
            return false;

        var owner = ComputeHoverOwner();
        return owner == candidate;
    }

    private SensorUI ComputeHoverOwner()
    {
        SensorUI top = null;
        int topIndex = int.MinValue;

        foreach (var sd in sensorDatas)
        {
            var ui = sd.sensorUI;
            if (ui == null || ui.pointerHoverArea == null) continue;
            if (!ui.gameObject.activeInHierarchy) continue;

            if (ui.pointerHoverArea.CheckIfHovering())
            {
                int idx = ui.gameObject.transform.GetSiblingIndex();
                if (idx > topIndex)
                {
                    topIndex = idx;
                    top = ui;
                }
            }
        }
        return top;
    }

    public (Vector2, Vector2) GetUIBoundaries()
    {
        if (!programStarted) return (Vector2.zero, Vector2.zero);
        if (viewTransformInitiated) return (uiViewMin, uiViewDims);
        if (uiCam == null) uiCam = GameObject.FindGameObjectWithTag("UICamera").GetComponent<Camera>();

        if (!uiCam.orthographic)
        {
            Debug.LogError("Main Camera is not orthographic.");
            return (Vector2.zero, Vector2.zero);
        }

        float size = uiCam.orthographicSize;
        float aspect = uiCam.aspect;

        float yMax = size;
        float yMin = -size;
        float xMax = size * aspect;
        float xMin = -size * aspect;

        uiViewMin = new(xMin, yMin);
        uiViewDims = new(xMax - xMin, yMax - yMin);

        viewTransformInitiated = true;

        return (uiViewMin, uiViewDims);
    }

    private void LerpGlobalBrightness(float deltaTime)
    {
        float settingsTint = main.SettingsViewDarkTintPercent * (isAnySensorSettingsViewActive ? 1f : 0f);
        bool allowPauseLerp = CheckAllowRestart();
        float pauseTint = allowPauseLerp ? main.PauseDarkTintPercent * (programPaused ? 1f : 0f) : 0f;

        float appliedTint = Mathf.Max(settingsTint, pauseTint);
        float target = 1f - appliedTint;

        if (globalBrightnessFactor == -1) globalBrightnessFactor = target;
        else globalBrightnessFactor = Mathf.Lerp(globalBrightnessFactor, target, deltaTime * main.GlobalSettingsViewChangeSpeed);
    }

    private void LerpTimeScale(float deltaTime)
    {
        float target = isAnySensorSettingsViewActive ? 0f : 1f;
        timeScale = Mathf.Lerp(timeScale, target, deltaTime * main.GlobalSettingsViewChangeSpeed);
    }

    private void LerpSensorUIScale(float deltaTime)
    {
        foreach (SensorData sensorData in sensorDatas)
        {
            Vector3 targetScale = sensorData.sensorUI.GetTotalScale(sensorData.isSettingsViewActive);
            Vector3 currentScale = sensorData.sensorUIObject.transform.localScale;

            Vector3 newScale = Vector3.Lerp(currentScale, targetScale, deltaTime * main.GlobalSettingsViewChangeSpeed);
            sensorData.sensorUIObject.transform.localScale = newScale;
        }
    }

    private void UpdatePauseTextAlpha(float deltaTime)
    {
        bool allowPauseLerp = CheckAllowRestart();
        float target = (allowPauseLerp && programPaused) ? 1f : 0f;

        if (pauseText == null && allowPauseLerp &&
            (programPaused || (pauseTextAlpha >= 0f && !Mathf.Approximately(pauseTextAlpha, target))))
        {
            var go = GameObject.FindGameObjectWithTag("PauseText");
            if (go != null)
            {
                pauseText = go.GetComponent<TMP_Text>();
                if (pauseText != null && pauseTextAlpha < 0f)
                {
                    // Initialize from current color
                    pauseTextAlpha = pauseText.color.a;
                }
            }
        }

        if (pauseText == null) return;

        if (pauseTextAlpha < 0f) pauseTextAlpha = pauseText.color.a;
        pauseTextAlpha = Mathf.Lerp(pauseTextAlpha, target, deltaTime * main.GlobalSettingsViewChangeSpeed);

        // Snap when close
        if (Mathf.Abs(pauseTextAlpha - target) < 0.001f) pauseTextAlpha = target;

        var c = pauseText.color;
        c.a = pauseTextAlpha;
        pauseText.color = c;
    }

    public Vector2 GetScreenToViewFactor(float resolutionAspect)
    {
        float boundsAspect = main.BoundaryDims.x / (float)main.BoundaryDims.y;

        float scaleX;
        float scaleY;
        if (boundsAspect > resolutionAspect)
        {
            // Scale Y down
            scaleY = resolutionAspect / boundsAspect;
            scaleX = 1.0f;
        }
        else
        {
            // Scale X down
            scaleX = boundsAspect / resolutionAspect;
            scaleY = 1.0f;
        }

        return new Vector2(scaleX, scaleY);
    }

    private (Vector2 ViewScale, Vector2 ViewOffset) GetViewTransform()
    {
        Vector2 boundaryDims = Utils.Int2ToVector2(main.BoundaryDims);
        float boundsAspect = boundaryDims.x / boundaryDims.y;
        float resolutionAspect = Resolution.x / Resolution.y;

        Vector2 scale = boundaryDims / Resolution;
        Vector2 offset = Vector2.zero;
        if (resolutionAspect > boundsAspect)
        {
            // Wider resolution: scale based on height
            scale.x = boundaryDims.y / Resolution.y;
            float scaledWidth = Resolution.x * scale.x;
            offset.x = (boundaryDims.x - scaledWidth) / 2.0f;
        }
        else
        {
            // Taller resolution: scale based on width
            scale.y = boundaryDims.x / Resolution.x;
            float scaledHeight = Resolution.y * scale.y;
            offset.y = (boundaryDims.y - scaledHeight) / 2.0f;
        }

        return (scale, offset);
    }

    private void SetResolutionData()
    {
        isStandardResolution = Resolution == StandardResolution;

        Vector2 uiCanvasResolution = GameObject.FindGameObjectWithTag("UICanvas").GetComponent<Canvas>().GetComponent<CanvasScaler>().referenceResolution;
        if (Utils.Int2ToVector2(main.DefaultResolution) != uiCanvasResolution) Debug.LogWarning("UICanvas reference resolution and resolution setting do not match. Rendering artifacts may appear.");

        float resolutionRatio = Resolution.x / Resolution.y;
        float boundaryDimsRatio = main.BoundaryDims.x / (float)main.BoundaryDims.y;
        float resBoundsratioDiff = Mathf.Abs(resolutionRatio / boundaryDimsRatio - 1);
        if (resBoundsratioDiff > 0.05)
        {
            Debug.LogWarning("Resolution ratio and BoundaryDims setting in Main do not match. Rendering artifacts may appear");
        }
    }

    private void SetStaticUIPositions()
    {
        Vector2 halfResolution = Resolution / 2.0f;
        Vector2 offset = halfResolution - halfResolution * ScreenToViewFactorUI;

        if (languageSelectDropdown != null)
        {
            languageSelectDropdown.localPosition = (Vector2)languageSelectDropdown.localPosition - offset;
        }
        foreach (UserUIElement userUIElement in userUIElements)
        {
            RectTransform rectTransform = userUIElement.GetComponent<RectTransform>();
            rectTransform.localPosition = (Vector2)rectTransform.localPosition - offset;
        }
    }

    public void SubscribeToActions()
    {
        OnSetNewPauseState += OnNewPauseState;
        OnSetNewSlowMotionState += OnNewSlowMotionState;
    }

    public void OnDestroy()
    {
        UnsubscribeFromActions();
        OnProgramUpdate = null;
        // Dispose ProgramManager-owned timers
        rapidFrameSteppingTimer?.Dispose();
        rapidFrameSteppingTimer = null;

        SetLastOpenedScene();

        // NOTE:
        // We no longer call userSettings.Reset() here, because OnDestroy also
        // executes on every scene reload during Play Mode.
        // Reset is now handled by an editor-only playmode hook below.
    }

    public void UnsubscribeFromActions()
    {
        OnSetNewPauseState -= OnNewPauseState;
        OnSetNewSlowMotionState -= OnNewSlowMotionState;
    }

    private void SetLastOpenedScene() => lastOpenedScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

    private void OnNewPauseState(bool state)
    {
        programPaused = state;
        Debug.Log(programPaused ? "Program paused" : "Program resumed");
        if (programPaused) notificationManager.OpenNotification("PauseTip");
        else notificationManager.CloseNotification("PauseTip");
    }

    private void OnNewSlowMotionState(bool state)
    {
        if (slowMotionActive == state) return;

        slowMotionActive = state;
        Debug.Log(slowMotionActive ? "Slow motion activated" : "Slow motion deactivated");

        if (slowMotionActive)
        {
            main.ProgramSpeed /= SlowMotionFactor;
            notificationManager.OpenNotification("SlowMotionTip");
        }
        else
        {
            main.ProgramSpeed *= SlowMotionFactor;
            notificationManager.CloseNotification("SlowMotionTip");
        }
    }

    public void SetNewLanguage(int languageIndex)
    {
        LocalizationSettings.SelectedLocale = LocalizationSettings.AvailableLocales.Locales[languageIndex];
        TriggerNewLanguageSelected();
    }

    private void TriggerNewLanguageSelected() => OnNewLanguageSelected?.Invoke();
    public void TriggerSetPauseState(bool state) => OnSetNewPauseState?.Invoke(state);
    public void TriggerSetSlowMotionState(bool state) => OnSetNewSlowMotionState?.Invoke(state);

    // ===== Resolution scale helpers =====

    public static float GetScaleFactor(Main.ResolutionScale scale)
    {
        switch (scale)
        {
            case Main.ResolutionScale.Scale_2_1: return 2.0f;
            case Main.ResolutionScale.Scale_1_1: return 1.0f;
            case Main.ResolutionScale.Scale_3_4: return 0.75f;
            case Main.ResolutionScale.Scale_2_3: return 2f / 3f;
            case Main.ResolutionScale.Scale_1_2: return 0.5f;
            case Main.ResolutionScale.Scale_1_3: return 1f / 3f;
            case Main.ResolutionScale.Scale_1_4: return 0.25f;
            default: return 1.0f;
        }
    }

    private void ApplyResolutionScale(Main.ResolutionScale desiredScale, bool forceSceneReset)
    {
        // Compute new scaled resolution
        float s = GetScaleFactor(desiredScale);
        int2 baseRes = main.DefaultResolution;
        int newW = Mathf.Max(1, Mathf.RoundToInt(baseRes.x * s));
        int newH = Mathf.Max(1, Mathf.RoundToInt(baseRes.y * s));
        int2 scaled = new int2(newW, newH);

        // Detect if anything changed
        bool changed =
            (ResolutionInt2.x != scaled.x || ResolutionInt2.y != scaled.y) ||
            (_appliedResolutionScale != desiredScale) ||
            (_appliedDefaultResolution.x != baseRes.x || _appliedDefaultResolution.y != baseRes.y);

        // Persist & mirror to Main so inspector/UI show the effective scale
        StoredResolutionScale = desiredScale;
        main.ResolutionScaleSetting = desiredScale;

        // Apply dims
        ResolutionInt2 = scaled;
        Resolution = Utils.Int2ToVector2(scaled);

        // Update dependent cached values
        SetResolutionData();

        // Record applied state
        _appliedResolutionScale = desiredScale;
        _appliedDefaultResolution = baseRes;

        // Reset scene if needed
        if (Application.isPlaying && changed && forceSceneReset && CheckAllowRestart())
        {
            sceneIsResetting = true;
            ResetScene();
        }
    }
}

#if UNITY_EDITOR
// ─────────────────────────────────────────────────────────────────────────────
// EDITOR-ONLY: Reset UserSettings once when exiting Play Mode (not on scene reload)
// ─────────────────────────────────────────────────────────────────────────────
[InitializeOnLoad]
static class ProgramManager_PlaymodeResetHook
{
    private static bool queuedReset;

    static ProgramManager_PlaymodeResetHook()
    {
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            // We are leaving play mode; queue reset to happen after we're back in edit mode.
            queuedReset = true;
        }
        else if (state == PlayModeStateChange.EnteredEditMode && queuedReset)
        {
            queuedReset = false;

            var pm = ProgramManager.Instance;
            if (pm != null && pm.userSettings != null)
            {
                pm.userSettings.Reset();
                EditorUtility.SetDirty(pm.userSettings);
                AssetDatabase.SaveAssets();
            }
        }
    }
}
#endif