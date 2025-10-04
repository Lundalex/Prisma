// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

Shader "UI/TranslucentImage-Paraform"
{
    Properties
    {
        [PerRendererData] _MainTex("Sprite Texture", 2D) = "white" {}

        [Header(Appearance)][Space]
        [KeywordEnum(Normal, Colorful, Opaque)] _BACKGROUND_MODE("Background Mode", int) = 0

        [Header(Refraction)][Space]
        [KeywordEnum(Off,On,Chromatic)] _REFRACTION_MODE("Refraction Mode", int) = 1
        // Singularity prevent reconstructions of control properties from _RefractiveIndexRatios
        //  so they need to be stored in the material
        [PowerSlider(20)] _RefractiveIndex("Refractive Index", Range(1, 4.5)) = 1.5
        [PowerSlider(2)] _ChromaticDispersion("Chromatic Dispersion", Range(0, 2)) = 0.5
        _RefractiveIndexRatios("Refractive Index Ratio", Vector) = (0.666666, 0.666666, 0.666666, 0)

        [Header(Edge Glint)][Space]
        [Toggle(_USE_EDGE_GLINT)] _USE_EDGE_GLINT("Enable Edge Glint", Float) = 0
        _EdgeGlintDirections("Edge Glint Directions", Vector) = (-0.258819, 0.9659259, 0.2588191, -0.9659258)
        _EdgeGlint1Strength("Edge Glint 1 Strength", Range(0, 1)) = .25
        _EdgeGlint2Strength("Edge Glint 2 Strength", Range(0, 1)) = .1
        _EdgeGlintWrap("Edge Glint Wrap", Float) = 1
        _EdgeGlintSharpness("Edge Glint Sharpness", Float) = 512

        //_Debug("Debug", Vector) = (0,0,0,0)

        [Header(Other)][Space]
        _StencilComp("Stencil Comparison", Float) = 8
        _Stencil("Stencil ID", Float) = 0
        _StencilOp("Stencil Operation", Float) = 0
        _StencilWriteMask("Stencil Write Mask", Float) = 255
        _StencilReadMask("Stencil Read Mask", Float) = 255

        _ColorMask("Color Mask", Float) = 15
        [HideInInspector]_Color ("Dummy Color", Color) = (1,1,1,1)

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"= "Transparent"
            "IgnoreProjector"= "True"
            "RenderType"= "Transparent"
            "PreviewType"= "Plane"
            "CanUseSpriteAtlas"= "True"
        }

        Stencil
        {
            Ref[_Stencil]
            Comp[_StencilComp]
            Pass[_StencilOp]
            ReadMask[_StencilReadMask]
            WriteMask[_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest[unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask[_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma exclude_renderers d3d11_9x gles

            #pragma vertex vertParaform
            #pragma fragment frag
            // #pragma enable_d3d11_debug_symbols

            #define _USE_PARAFORM true

            #include "../../TranslucentImage/Resources/Shaders/TranslucentImage.hlsl"
            #include "paraform.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #pragma shader_feature_local_fragment _BACKGROUND_MODE_NORMAL _BACKGROUND_MODE_COLORFUL _BACKGROUND_MODE_OPAQUE
            #pragma shader_feature_local_fragment __ _USE_EDGE_GLINT
            #pragma shader_feature_local_fragment _REFRACTION_MODE_OFF _REFRACTION_MODE_ON _REFRACTION_MODE_CHROMATIC

            VertexOutput vertParaform(VertexInput IN)
            {
                VertexOutput OUT = vert(IN);
                unpackVertexDataParaform(IN, OUT);
                return OUT;
            }

            half4 frag(VertexOutput IN) : SV_Target
            {
                half2      screenPos;
                half4      foregroundColor;
                Appearance appearance;
                fragSetup(IN, screenPos, foregroundColor, appearance);

                ParaformConfig paraformConfig = createParaformConfig(IN);
                half4          color = paraformMain(paraformConfig, screenPos, foregroundColor, appearance);

                fragFinish(IN, screenPos, color);

                // color.rgb += (hash12(screenPos.xy * _ScreenParams.xy) - .5) * .025;
                // color.rgb = specular(normal, _EdgeGlintDirections.xy);
                // half2 offset = GetRefractedScreenOffset(normal, 1 / 1.5);
                // color.rgb = float4(lerp(float3(1, 0, 0), float3(0, 1, 1), (offset.y + 1) / 2), 1) * color.a;
                // color.rgb = float3(((normal + 1) / 2));
                // color.rgb = dot(normal, calcHalfDir(_EdgeGlintDirections.xy));
                // color.rgb = -normal.z;
                // color.rgb = blendEdge(.5, normal);
                // color.rgb = float3(abs(offset), 0);
                // color.rgb = F;

                // half3 box = roundedBox(IN.boxData.xy, IN.boxData.zw, radii, _CornerCurvature);
                // color.rgb = sdfViz(box.x / max(IN.boxData.z, IN.boxData.w));
                // color.rgb = float3((box.yz + 1) / 2, 0);
                // color.a = 1;

                // color.rgb = 1;
                // color.rgb *= color.a;

                return color;
            }
            ENDCG
        }
    }

    CustomEditor "LeTai.Asset.TranslucentImage.Editor.TranslucentImageShaderGUI"
}
