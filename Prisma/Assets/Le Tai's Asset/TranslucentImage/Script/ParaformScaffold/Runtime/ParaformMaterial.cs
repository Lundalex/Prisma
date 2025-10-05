// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using UnityEngine;

namespace LeTai.Paraform.Scaffold
{
public static class ParaformMaterial
{
    public static class ShaderID
    {
        public static readonly int G_CANVAS_SCALE_FACTOR = Shader.PropertyToID("_LeTai_CanvasScaleFactor");

        public const           string REFRACTION_MODE_OFF        = "_REFRACTION_MODE_OFF";
        public const           string REFRACTION_MODE_ON         = "_REFRACTION_MODE_ON";
        public const           string REFRACTION_MODE_CHROMATIC  = "_REFRACTION_MODE_CHROMATIC";
        public static readonly int    REFRACTIVE_INDEX_DUMMY     = Shader.PropertyToID("_RefractiveIndex");
        public static readonly int    CHROMATIC_DISPERSION_DUMMY = Shader.PropertyToID("_ChromaticDispersion");
        public static readonly int    REFRACTIVE_INDEX_RATIOS    = Shader.PropertyToID("_RefractiveIndexRatios");

        public const           string USE_EDGE_GLINT           = "_USE_EDGE_GLINT";
        public static readonly int    EDGE_GLINT_DIRECTIONS    = Shader.PropertyToID("_EdgeGlintDirections");
        public static readonly int    EDGE_GLINT1_STRENGTH     = Shader.PropertyToID("_EdgeGlint1Strength");
        public static readonly int    EDGE_GLINT2_STRENGTH     = Shader.PropertyToID("_EdgeGlint2Strength");
        public static readonly int    EDGE_GLINT_WRAP_RAW      = Shader.PropertyToID("_EdgeGlintWrap");
        public static readonly int    EDGE_GLINT_SHARPNESS_RAW = Shader.PropertyToID("_EdgeGlintSharpness");
    }

    const float NA_D_LINE_UM = 0.5893f;
    const float HED_LINE_UM  = 0.58756f;
    const float F_LINE_UM    = 0.48613f;
    const float C_LINE_UM    = 0.65627f;

    static readonly Vector3 PRIMARIES_UM = new(.630f, .532f, .467f);

    static readonly float REF_COEFF = InvSq(NA_D_LINE_UM);
    static readonly Vector3 PRIMARIES_COEFF = new(InvSq(PRIMARIES_UM[0]),
                                                  InvSq(PRIMARIES_UM[1]),
                                                  InvSq(PRIMARIES_UM[2]));
    static readonly float DISPERSION_FC       = InvSq(F_LINE_UM) - InvSq(C_LINE_UM);
    static readonly float DISPERSION_HED_NA_D = InvSq(HED_LINE_UM) - REF_COEFF;

    public static Vector3 GetRefractiveIndexRatios(float iorAtNaDLine, float abbeRcp)
    {
        iorAtNaDLine = Mathf.Max(1, iorAtNaDLine);
        abbeRcp      = Mathf.Max(0, abbeRcp);

        if (abbeRcp == 0)
            return new Vector3(1f / iorAtNaDLine,
                               1f / iorAtNaDLine,
                               1f / iorAtNaDLine);

        float cauchyB = (iorAtNaDLine - 1f) * abbeRcp / (DISPERSION_FC - abbeRcp * DISPERSION_HED_NA_D);
        float cauchyA = iorAtNaDLine - cauchyB * REF_COEFF;

        var iors = new Vector3(cauchyA + cauchyB * PRIMARIES_COEFF[0],
                               cauchyA + cauchyB * PRIMARIES_COEFF[1],
                               cauchyA + cauchyB * PRIMARIES_COEFF[2]);
        return new Vector3(1f / iors[0],
                           1f / iors[1],
                           1f / iors[2]);
    }

    static float InvSq(float x)
    {
        return 1f / (x * x);
    }

    public static void SetDispersion(Material material, float dispersion)
    {
        var ior = material.GetFloat(ShaderID.REFRACTIVE_INDEX_DUMMY);
        material.SetFloat(ShaderID.CHROMATIC_DISPERSION_DUMMY, dispersion);
        material.SetVector(ShaderID.REFRACTIVE_INDEX_RATIOS, GetRefractiveIndexRatios(ior, dispersion));
    }

    public static void SetRefractiveIndex(Material material, float refractiveIndex)
    {
        var dispersion = material.GetFloat(ShaderID.CHROMATIC_DISPERSION_DUMMY);
        material.SetFloat(ShaderID.REFRACTIVE_INDEX_DUMMY, refractiveIndex);
        material.SetVector(ShaderID.REFRACTIVE_INDEX_RATIOS, GetRefractiveIndexRatios(refractiveIndex, dispersion));
    }

    public static void SetRefractiveIndexRatios(Material material, float refractiveIndex, float chromaticDispersion)
    {
        material.SetFloat(ShaderID.REFRACTIVE_INDEX_DUMMY,     refractiveIndex);
        material.SetFloat(ShaderID.CHROMATIC_DISPERSION_DUMMY, chromaticDispersion);
        material.SetVector(ShaderID.REFRACTIVE_INDEX_RATIOS, GetRefractiveIndexRatios(refractiveIndex,
                                                                                      chromaticDispersion));
    }

    const float EDGE_GLINT_WRAP_SCALE = 20;
    const float EDGE_GLINT_WRAP_POWER = .25f;

    public static float EdgeGlintWrapToRaw(float edgeGlintWrap)
    {
        return (1 - Mathf.Pow(edgeGlintWrap, EDGE_GLINT_WRAP_POWER)) * EDGE_GLINT_WRAP_SCALE + 1;
    }

    public static float EdgeGlintWrapFromRaw(float edgeGlintWrapRaw)
    {
        return Mathf.Pow((EDGE_GLINT_WRAP_SCALE - edgeGlintWrapRaw + 1) / EDGE_GLINT_WRAP_SCALE,
                         1f / EDGE_GLINT_WRAP_POWER);
    }

    public static float GetEdgeGlintWrap(Material material)
    {
        return EdgeGlintSharpnessFromRaw(material.GetFloat(ShaderID.EDGE_GLINT_WRAP_RAW));
    }

    public static void SetEdgeGlintWrap(Material material, float edgeGlintWrap)
    {
        material.SetFloat(ShaderID.EDGE_GLINT_WRAP_RAW, EdgeGlintSharpnessToRaw(edgeGlintWrap));
    }

    public static float EdgeGlintSharpnessToRaw(float edgeGlintSharpness)
    {
        return 2 / Mathf.Pow(1 - edgeGlintSharpness, 4);
    }

    public static float EdgeGlintSharpnessFromRaw(float edgeGlintSharpnessRaw)
    {
        return 1 - Mathf.Pow(2 / edgeGlintSharpnessRaw, 1f / 4f);
    }

    public static float GetEdgeGlintSharpness(Material material)
    {
        return EdgeGlintSharpnessFromRaw(material.GetFloat(ShaderID.EDGE_GLINT_SHARPNESS_RAW));
    }

    public static void SetEdgeGlintSharpness(Material material, float edgeGlintSharpness)
    {
        material.SetFloat(ShaderID.EDGE_GLINT_SHARPNESS_RAW, EdgeGlintSharpnessToRaw(edgeGlintSharpness));
    }
}
}
