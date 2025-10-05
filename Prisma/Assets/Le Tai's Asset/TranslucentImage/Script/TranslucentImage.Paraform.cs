// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System.Diagnostics;
using LeTai.Common;
using LeTai.Paraform;
using LeTai.Paraform.Scaffold;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
public partial class TranslucentImage
{
    public ParaformConfig paraformConfig = ParaformConfig.DEFAULT;

    float etaCache      = 1 / 1.5f;
    float previousScale = 0;

    private static float GetMaxRefractionOffset(float minDistance, float eta)
    {
        float k      = 1.0f - eta * eta;
        float offset = Mathf.Sqrt(k) * minDistance / eta;
        return offset;
    }

    [Conditional("LETAI_PARAFORM")]
    private void PadRectForRefraction(ref Rect rect)
    {
        var offset = GetMaxRefractionOffset(paraformConfig.Elevation, etaCache);
        var padX   = Mathf.Max(0, offset - rect.width);
        var padY   = Mathf.Max(0, offset - rect.height);
        rect.x      -= padX / 2;
        rect.y      -= padY / 2;
        rect.width  += padX;
        rect.height += padY;
    }

    [Conditional("LETAI_PARAFORM")]
    private void CacheEta()
    {
        var etas = material.GetVector(ShaderID.REFRACTIVE_INDEX_RATIOS);
        etaCache = Mathf.Min(etas.x, Mathf.Min(etas.y, etas.z));
    }

    [Conditional("LETAI_PARAFORM")]
    void SetParaformShaderGlobal()
    {
        if (!canvas) // trigger on undo
            return;

        Shader.SetGlobalFloat(ShaderID.G_CANVAS_SCALE_FACTOR, canvas.scaleFactor);
    }

    [Conditional("LETAI_PARAFORM")]
    void LateUpdate()
    {
        var localScale = rectTransform.localScale;
        // var scale      = (localScale.x + localScale.y) / 2f;
        var scale = Mathf.Max(localScale.x, localScale.y);
        if (Mathf.Abs(scale - previousScale) > 1e-5f)
        {
            SetVerticesDirty();
            previousScale = scale;
        }
    }

    [Conditional("LETAI_PARAFORM")]
    public static void CopyParaformMaterialPropertiesTo(Material src, Material dst)
    {
        MaterialUtils.CopyKeyword(src, dst, ShaderID.REFRACTION_MODE_OFF);
        MaterialUtils.CopyKeyword(src, dst, ShaderID.REFRACTION_MODE_ON);
        MaterialUtils.CopyKeyword(src, dst, ShaderID.REFRACTION_MODE_CHROMATIC);
        MaterialUtils.CopyKeyword(src, dst, ShaderID.USE_EDGE_GLINT);

        MaterialUtils.CopyFloat(src, dst, ShaderID.REFRACTIVE_INDEX_DUMMY);
        MaterialUtils.CopyFloat(src, dst, ShaderID.CHROMATIC_DISPERSION_DUMMY);
        MaterialUtils.CopyVector(src, dst, ShaderID.REFRACTIVE_INDEX_RATIOS);

        MaterialUtils.CopyVector(src, dst, ShaderID.EDGE_GLINT_DIRECTIONS);
        MaterialUtils.CopyFloat(src, dst, ShaderID.EDGE_GLINT1_STRENGTH);
        MaterialUtils.CopyFloat(src, dst, ShaderID.EDGE_GLINT2_STRENGTH);
        MaterialUtils.CopyFloat(src, dst, ShaderID.EDGE_GLINT_WRAP_RAW);
        MaterialUtils.CopyFloat(src, dst, ShaderID.EDGE_GLINT_SHARPNESS_RAW);
    }

#if LETAI_PARAFORM
    public override bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtilityPatch.ScreenPointToLocalPointInRectangle(rectTransform, screenPoint, eventCamera, out var local))
            return false;

        Rect rect = GetPixelAdjustedRect();

        var padding = raycastPadding;
        rect.xMin += padding.x;
        rect.yMin += padding.y;
        rect.xMax -= padding.z;
        rect.yMax -= padding.w;

        if (!ParaformUtils.IsRaycastLocationValid(paraformConfig, rect, local))
            return false;

        return base.IsRaycastLocationValid(screenPoint, eventCamera);
    }

    protected override void OnDidApplyAnimationProperties()
    {
        SetVerticesDirty();
        base.OnDidApplyAnimationProperties();
    }

    /// <summary>
    /// Convenience wrapper for <see cref="ParaformMaterial.SetDispersion"/>.
    /// For better performance, use that instead and manage the material yourself.
    /// </summary>
    public void SetDispersionSlow(float dispersion)
    {
        ParaformMaterial.SetDispersion(materialForRendering, dispersion);
    }

    /// <summary>
    /// Convenience wrapper for <see cref="ParaformMaterial.SetRefractiveIndex"/>.
    /// For better performance, use that instead and manage the material yourself.
    /// </summary>
    public void SetRefractiveIndexSlow(float refractiveIndex)
    {
        ParaformMaterial.SetRefractiveIndex(materialForRendering, refractiveIndex);
    }

    /// <summary>
    /// Convenience wrapper for <see cref="ParaformMaterial.SetRefractiveIndexRatios"/>.
    /// For better performance, use that instead and manage the material yourself.
    /// </summary>
    public void SetRefractiveIndexRatiosSlow(float refractiveIndex, float chromaticDispersion)
    {
        ParaformMaterial.SetRefractiveIndexRatios(materialForRendering, refractiveIndex, chromaticDispersion);
    }

    /// <summary>
    /// Convenience wrapper for <see cref="ParaformMaterial.SetEdgeGlintWrap"/>.
    /// For better performance, use that instead and manage the material yourself.
    /// </summary>
    public void SetEdgeGlintWrap(float edgeGlintWrapNormalized)
    {
        ParaformMaterial.SetEdgeGlintWrap(materialForRendering, edgeGlintWrapNormalized);
    }

    /// <summary>
    /// Convenience wrapper for <see cref="ParaformMaterial.SetEdgeGlintSharpness"/>.
    /// For better performance, use that instead and manage the material yourself.
    /// </summary>
    public void SetEdgeGlintSharpness(float edgeGlintSharpnessNormalized)
    {
        ParaformMaterial.SetEdgeGlintSharpness(materialForRendering, edgeGlintSharpnessNormalized);
    }
#endif
}
}
