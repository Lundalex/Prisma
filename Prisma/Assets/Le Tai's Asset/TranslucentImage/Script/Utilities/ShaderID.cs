using LeTai.Paraform.Scaffold;
using UnityEngine;

namespace LeTai.Asset.TranslucentImage
{
public static class ShaderID
{
    public static readonly int BLUR_TEX         = Shader.PropertyToID("_BlurTex");
    public static readonly int MAIN_TEX         = Shader.PropertyToID("_MainTex");
    public static readonly int RADIUS           = Shader.PropertyToID("_Radius");
    public static readonly int BACKGROUND_COLOR = Shader.PropertyToID("_BackgroundColor");
    public static readonly int CROP_REGION      = Shader.PropertyToID("_CropRegion");
    // public static readonly int ENV_TEX      = Shader.PropertyToID("_EnvTex");

    public const string KW_BACKGROUND_MODE_COLORFUL = "_BACKGROUND_MODE_COLORFUL";
    public const string KW_BACKGROUND_MODE_NORMAL   = "_BACKGROUND_MODE_NORMAL";
    public const string KW_BACKGROUND_MODE_OPAQUE   = "_BACKGROUND_MODE_OPAQUE";

    public static readonly int G_CANVAS_SCALE_FACTOR = ParaformMaterial.ShaderID.G_CANVAS_SCALE_FACTOR;

    public const           string REFRACTION_MODE_OFF        = ParaformMaterial.ShaderID.REFRACTION_MODE_OFF;
    public const           string REFRACTION_MODE_ON         = ParaformMaterial.ShaderID.REFRACTION_MODE_ON;
    public const           string REFRACTION_MODE_CHROMATIC  = ParaformMaterial.ShaderID.REFRACTION_MODE_CHROMATIC;
    public static readonly int    REFRACTIVE_INDEX_DUMMY     = ParaformMaterial.ShaderID.REFRACTIVE_INDEX_DUMMY;
    public static readonly int    CHROMATIC_DISPERSION_DUMMY = ParaformMaterial.ShaderID.CHROMATIC_DISPERSION_DUMMY;
    public static readonly int    REFRACTIVE_INDEX_RATIOS    = ParaformMaterial.ShaderID.REFRACTIVE_INDEX_RATIOS;

    public const           string USE_EDGE_GLINT           = ParaformMaterial.ShaderID.USE_EDGE_GLINT;
    public static readonly int    EDGE_GLINT_DIRECTIONS    = ParaformMaterial.ShaderID.EDGE_GLINT_DIRECTIONS;
    public static readonly int    EDGE_GLINT1_STRENGTH     = ParaformMaterial.ShaderID.EDGE_GLINT1_STRENGTH;
    public static readonly int    EDGE_GLINT2_STRENGTH     = ParaformMaterial.ShaderID.EDGE_GLINT2_STRENGTH;
    public static readonly int    EDGE_GLINT_WRAP_RAW      = ParaformMaterial.ShaderID.EDGE_GLINT_WRAP_RAW;
    public static readonly int    EDGE_GLINT_SHARPNESS_RAW = ParaformMaterial.ShaderID.EDGE_GLINT_SHARPNESS_RAW;
}
}
