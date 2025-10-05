using System;
using System.Diagnostics.CodeAnalysis;
using LeTai.Common;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace LeTai.Asset.TranslucentImage
{
/// <summary>
/// Dynamic blur-behind UI element
/// </summary>
[HelpURL("https://leloctai.com/asset/translucentimage/docs/articles/customize.html#translucent-image")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public partial class TranslucentImage : Image, IActiveRegionProvider, IMeshModifier
{
    /// <summary>
    /// Source of the blurred background for this image
    /// </summary>
    public TranslucentImageSource source
    {
        get => _source;
        set
        {
            _source = value;

            // We need a separated variable, as the backing field is set before the setter is called from EditorProperty
            if (_source == _sourcePrev)
                return;

            DisconnectSource(_sourcePrev);
            ConnectSource(_source);
            _sourcePrev = source;
        }
    }

    [Obsolete("Use foregroundOpacity instead")]
    public float spriteBlending
    {
        get => foregroundOpacity;
        set => foregroundOpacity = value;
    }

    /// <summary>
    /// How much Sprite and Color contribute to the Image. Use this instead of Color.alpha
    /// </summary>
    public float foregroundOpacity
    {
        get => _foregroundOpacity;
        set
        {
            _foregroundOpacity = value;
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// (De)Saturate the image, 1 is normal, 0 is grey scale, below zero make the image negative
    /// </summary>
    public float vibrancy
    {
        get => _vibrancy;
        set
        {
            _vibrancy = value;
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// In Normal Background Mode: Brighten/darken the background. In Colorful Background Mode: Set the background overall brightness.
    /// </summary>
    public float brightness
    {
        get => _brightness;
        set
        {
            _brightness = value;
            SetVerticesDirty();
        }
    }

    /// <summary>
    /// Flatten the color behind to maintain color contrast on varying backgrounds
    /// </summary>
    public float flatten
    {
        get => _flatten;
        set
        {
            _flatten = value;
            SetVerticesDirty();
        }
    }

    public override Material material
    {
        get => base.material;
        set
        {
            base.material = value;
            OnDirtyMaterial();
        }
    }

    public override Material defaultMaterial
    {
        get
        {
#if LETAI_PARAFORM
            return DefaultResources.Instance.paraformMaterial;
#else
            return DefaultResources.Instance.material;
#endif
        }
    }

    [FormerlySerializedAs("source")]
    [Tooltip("Source of the blurred background for this image")]
    [SerializeField] TranslucentImageSource _source;

    [FormerlySerializedAs("spriteBlending")]
    [FormerlySerializedAs("m_spriteBlending")]
    [Tooltip("How much Sprite and Color contribute to the Image. Use this instead of Color.alpha")]
    [SerializeField] [Range(0, 1)] float _foregroundOpacity = .5f;
    [FormerlySerializedAs("vibrancy")]
    [Tooltip("(De)Saturate the image, 1 is normal, 0 is grey scale, below zero make the image negative")]
    [SerializeField] [Range(-1, 2)] float _vibrancy = 1;
    [FormerlySerializedAs("brightness")]
    [Tooltip("In Normal Background Mode: Brighten/darken the background. In Colorful Background Mode: Set the background overall brightness.")]
    [SerializeField] [Range(-1, 1)] float _brightness = 0;
    [FormerlySerializedAs("flatten")]
    [Tooltip("Flatten the color behind to maintain color contrast on varying backgrounds")]
    [SerializeField] [Range(0, 1)] float _flatten = 0;

    bool                   shouldRun;
    bool                   isBirp;
    TranslucentImageSource _sourcePrev;

    protected override void Start()
    {
        isBirp = !GraphicsSettings.currentRenderPipeline;

        AutoAcquireSource();

        if (material && source)
        {
            material.SetTexture(ShaderID.BLUR_TEX, source.BlurredScreen);
        }

        m_OnDirtyMaterialCallback += OnDirtyMaterial;
        if (canvas)
            canvas.additionalShaderChannels |= AdditionalCanvasShaderChannels.TexCoord1
                                             | AdditionalCanvasShaderChannels.TexCoord2
                                             | AdditionalCanvasShaderChannels.TexCoord3;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        SetVerticesDirty();

        ConnectSource(source);
        _sourcePrev = source;

        paraformConfig.changed    += ParaformConfigChanged;
        Canvas.willRenderCanvases += OnWillRenderCanvases;

#if UNITY_EDITOR
        if (!UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Start();
        }

        UnityEditor.Undo.undoRedoPerformed += OnUndoRedoPerformed;
#endif
    }

    void ParaformConfigChanged()
    {
        SetVerticesDirty();
        UpdateTrueShadowHash();
    }

    protected override void OnDisable()
    {
        SetVerticesDirty();
        base.OnDisable();

        paraformConfig.changed    -= ParaformConfigChanged;
        Canvas.willRenderCanvases -= OnWillRenderCanvases;

        DisconnectSource(source);
#if UNITY_EDITOR
        UnityEditor.Undo.undoRedoPerformed -= OnUndoRedoPerformed;
#endif
    }

    void OnWillRenderCanvases()
    {
        SetParaformShaderGlobal();
    }

//     void Update()
//     {
// #if DEBUG
//         if (Application.isPlaying && !IsInPrefabMode())
//         {
//             if (!source)
//                 Debug.LogWarning("TranslucentImageSource is missing. " +
//                                  "Please add the TranslucentImageSource component to your main camera, " +
//                                  "then assign it to the Source field of the Translucent Image(s)");
//         }
// #endif
//     }

    public bool HaveActiveRegion()
    {
        return (bool)this && IsActive() && canvas && canvas.enabled;
    }

    public void GetActiveRegion(VPMatrixCache vpMatrixCache, out ActiveRegion activeRegion)
    {
        VPMatrixCache.Index vpMatrixIndex;
        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            vpMatrixIndex = VPMatrixCache.Index.INVALID;
        }
        else
        {
            var refCamera = canvas.worldCamera;
            if (!refCamera)
            {
                Debug.LogError("Translucent Image need an Event Camera for World Space Canvas");
                vpMatrixIndex = VPMatrixCache.Index.INVALID;
            }
            else
            {
                vpMatrixIndex = vpMatrixCache.IndexOf(refCamera);
                if (!vpMatrixIndex.IsValid())
                    vpMatrixIndex = vpMatrixCache.Add(refCamera);
            }
        }

        var rect = rectTransform.rect;
        PadRectForRefraction(ref rect);

        activeRegion = new ActiveRegion(rect,
                                        rectTransform.localToWorldMatrix,
                                        vpMatrixIndex);
    }

    /// <summary>
    /// Copy material keywords state and properties, except stencil properties
    /// </summary>
    public static void CopyMaterialPropertiesTo(Material src, Material dst)
    {
        MaterialUtils.CopyKeyword(src, dst, ShaderID.KW_BACKGROUND_MODE_COLORFUL);
        MaterialUtils.CopyKeyword(src, dst, ShaderID.KW_BACKGROUND_MODE_NORMAL);
        MaterialUtils.CopyKeyword(src, dst, ShaderID.KW_BACKGROUND_MODE_OPAQUE);

        CopyParaformMaterialPropertiesTo(src, dst);
    }

    void ConnectSource(TranslucentImageSource source)
    {
        if (!source) return;

        source.RegisterActiveRegionProvider(this);
        source.blurredScreenChanged += SetBlurTex;
        source.blurRegionChanged    += SetBlurRegion;
        SetBlurTex();
        SetBlurRegion();
    }

    void DisconnectSource(TranslucentImageSource source)
    {
        if (!source) return;

        source.UnRegisterActiveRegionProvider(this);
        source.blurredScreenChanged -= SetBlurTex;
        source.blurRegionChanged    -= SetBlurRegion;
    }

    void SetBlurTex()
    {
        if (!source)
            return;

        materialForRendering.SetTexture(ShaderID.BLUR_TEX, source.BlurredScreen);
    }

    void SetBlurRegion()
    {
        if (
            !source
         || !canvas
         || !canvas.enabled
        )
            return;

        if (isBirp || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            var minMaxVector = RectUtils.ToMinMaxVector(source.BlurRegionNormalizedScreenSpace);
            materialForRendering.SetVector(ShaderID.CROP_REGION, minMaxVector);
        }
        else
        {
            materialForRendering.SetVector(ShaderID.CROP_REGION, RectUtils.ToMinMaxVector(source.BlurRegion));
        }
    }

    void OnDirtyMaterial()
    {
        SetBlurTex();
        SetBlurRegion();

        CacheEta();
    }

    bool IsInPrefabMode()
    {
#if !UNITY_EDITOR
        return false;
#else // UNITY_EDITOR
#if UNITY_2021_2_OR_NEWER
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#else
        var stage = UnityEditor.Experimental.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
#endif
        return stage != null;
#endif // !UNITY_EDITOR
    }

    bool sourceAcquiredOnStart = false;

    void AutoAcquireSource()
    {
        if (IsInPrefabMode()) return;
        if (sourceAcquiredOnStart) return;

        source                = source ? source : Shims.FindObjectOfType<TranslucentImageSource>();
        sourceAcquiredOnStart = true;
    }

    void OnUndoRedoPerformed()
    {
        source = _source;
        OnDirtyMaterial();
        UpdateTrueShadowHash();
    }

    void WriteVertexData(ref SpanWriter<float> writer)
    {
        writer.Write(Packing.FloatPacker.Uniform(8)
                            .Enqueue(foregroundOpacity, 1)
                            .Enqueue(flatten,           1)
        );
        writer.Write(Packing.FloatPacker.Uniform(10)
                            .Enqueue(vibrancy,   -1, 2)
                            .Enqueue(brightness, -1, 1)
        );
    }

    public virtual void ModifyMesh(VertexHelper vh)
    {
        ListPool<UIVertex>.Get(out var vertices);
        vh.GetUIVertexStream(vertices);

        Span<float> data   = stackalloc float[4];
        var         writer = SpanUtils.WriterFor(data);

#if LETAI_PARAFORM
        Span<float> dataParaform         = stackalloc float[4 * 2];
        var         writerParaformCommon = SpanUtils.WriterFor(dataParaform[..^2]);
        var         writerParaformVertex = SpanUtils.WriterFor(dataParaform[^2..]);
        var         paraformEncoder      = new Paraform.ParaformVertexDataEncoder(rectTransform, paraformConfig);
#endif

        WriteVertexData(ref writer);
        // writer.FillRest();
        var uv1 = SpanUtils.ToVector4(data);

#if LETAI_PARAFORM
        paraformEncoder.WriteCommon(ref writerParaformCommon);
        var uv2 = SpanUtils.ToVector4(dataParaform[..4]);
#endif

        for (var i = 0; i < vertices.Count; i++)
        {
            UIVertex vert = vertices[i];

            vert.uv1 = uv1;

#if LETAI_PARAFORM
            vert.uv2 = uv2;
            paraformEncoder.WritePerVertex(ref writerParaformVertex, vert.position);
            vert.uv3 = SpanUtils.ToVector4(dataParaform[4..8]);
            writerParaformVertex.Reset();
#endif

            vertices[i] = vert;
        }

        vh.Clear();
        vh.AddUIVertexTriangleStream(vertices);
        // ParaformVertexDataEncoder.ModifyMesh(this, paraformConfig, vh);
    }

    public virtual void ModifyMesh(Mesh mesh)
    {
        using var vh = new VertexHelper(mesh);

        ModifyMesh(vh);
        vh.FillMesh(mesh);
    }
}
}
