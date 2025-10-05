#pragma once

#include "UnityUI.cginc"
// UI shaders still use birp texture convention
#include "interop_birp.cginc"
#include "common.hlsl"
#include "packing.hlsl"
#include "blending.hlsl"

uniform sampler2D _MainTex;
uniform fixed4    _TextureSampleAdd;
uniform float4    _ClipRect;
uniform float4    _MainTex_ST;
uniform float     _UIMaskSoftnessX;
uniform float     _UIMaskSoftnessY;
uniform int       _UIVertexColorAlwaysGammaSpace;
UNITY_DECLARE_SCREENSPACE_TEXTURE(_BlurTex);

uniform float4 _CropRegion; //xMin, yMin, xMax, yMax

uniform half4 _Debug;

struct VertexInput
{
    float4 vertex : POSITION;
    half4  color : COLOR;
    float2 texcoord : TEXCOORD0;
    float4 packedData1 : TEXCOORD1;
    #ifdef _USE_PARAFORM
    float4 packedData2 : TEXCOORD2;
    float4 packedData3 : TEXCOORD3;
    #endif
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct VertexOutput
{
    float4 vertex : SV_POSITION;
    half4  color : COLOR;
    float4 mask : TEXCOORD0;
    float2 texcoord : TEXCOORD1;
    float4 worldPosition : TEXCOORD2;
    float4 screenPos : TEXCOORD3;
    float4 transfer1 : TEXCOORD4;
    #ifdef _USE_PARAFORM
    half4 transfer2 : TEXCOORD5;
    half4  transfer3 : TEXCOORD6;
    float4  transfer4 : TEXCOORD7;  // need float for hq derivative on some opengl driver
    #endif
    UNITY_VERTEX_OUTPUT_STEREO
};

struct Appearance
{
    half foregroundOpacity;
    half vibrancy;
    half brightness;
    half flatten;
};

void unpackVertexData(VertexInput i, inout VertexOutput o)
{
    FloatUnpacker upk0 = CreateUnpacker(i.packedData1[0], 8);
    FloatUnpacker upk1 = CreateUnpacker(i.packedData1[1], 10);
    o.transfer1 = float4(
        DequeueNonNegative(upk0, 1),
        Dequeue(upk1, -1, 2),
        Dequeue(upk1, -1, 1),
        DequeueNonNegative(upk0, 1)
    );
}

Appearance createAppearance(float4 transfer1)
{
    Appearance appearance;
    appearance.foregroundOpacity = transfer1[0];
    appearance.vibrancy = transfer1[1];
    appearance.brightness = transfer1[2];
    appearance.flatten = transfer1[3];
    return appearance;
}

VertexOutput vert(VertexInput IN)
{
    VertexOutput OUT = (VertexOutput)0;

    UNITY_SETUP_INSTANCE_ID(IN);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

    OUT.worldPosition = IN.vertex;

    float4 clipPos = UnityObjectToClipPos(IN.vertex);
    OUT.vertex = clipPos;

    float2 pixelSize = clipPos.w;
    pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));
    float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
    OUT.mask = float4(IN.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

    if (_UIVertexColorAlwaysGammaSpace && !IsGammaSpace())
    {
        IN.color.rgb = UIGammaToLinearShim(IN.color.rgb);
    }
    OUT.color = IN.color;

    OUT.texcoord = IN.texcoord;

    OUT.screenPos = ComputeNonStereoScreenPos(OUT.vertex);
    #if UNITY_VERSION >= 202120 && UNITY_UV_STARTS_AT_TOP
    if (_ProjectionParams.x > 0 && unity_MatrixVP[1][1] < 0)
        OUT.screenPos.y = OUT.screenPos.w - OUT.screenPos.y;
    #endif

    unpackVertexData(IN, OUT);

    return OUT;
}

void fragSetup(inout VertexOutput IN, out half2 screenPos, out half4 foregroundColor, out Appearance appearance)
{
    //Round up the alpha color coming from the interpolator (to 1.0/256.0 steps)
    //The incoming alpha could have numerical instability, which makes it very sensible to
    //HDR color transparency blend, when it blends with the world's texture.
    const half alphaPrecision = half(0xff);
    const half invAlphaPrecision = half(1.0 / alphaPrecision);
    IN.color.a = round(IN.color.a * alphaPrecision) * invAlphaPrecision;

    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

    screenPos = IN.screenPos.xy / IN.screenPos.w;
    foregroundColor = tex2D(_MainTex, IN.texcoord.xy) + _TextureSampleAdd;
    foregroundColor *= IN.color;

    appearance = createAppearance(IN.transfer1);
}

void fragFinish(VertexOutput IN, half2 screenPos, inout half4 color)
{
    // TODO: fix Shadow and Outline tinting somehow
    // Multiplying last so glints work
    // color *= IN.color;

    #ifdef UNITY_UI_CLIP_RECT
    half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.mask.xy)) * IN.mask.zw);
    color.a *= m.x * m.y;
    #endif


    #ifdef UNITY_UI_ALPHACLIP
    clip(color.a - 0.001);
    #endif

    color.rgb += noise(screenPos.xy * _ScreenParams.xy) * 1. / 255.;

    color.rgb *= color.a;
}

half3 sampleBackground(float2 screenPos)
{
    half2 blurTexcoord = CropUV(screenPos, _CropRegion);
    half3 backgroundColor = SAMPLE_SCREEN_TEX(_BlurTex, blurTexcoord).rgb;
    return backgroundColor;
}

half4 blendBackground(half4 foregroundColor, half3 backgroundColor, Appearance appearance)
{
    half4 color;
    color.a = foregroundColor.a;

    #if _BACKGROUND_MODE_NORMAL
    backgroundColor = saturate(backgroundColor + (.5 - backgroundColor) * appearance.flatten);
    backgroundColor = saturate(lerp(LinearRgbToLuminance(backgroundColor), backgroundColor, appearance.vibrancy));
    backgroundColor = saturate(backgroundColor + appearance.brightness);
    color.rgb = lerp(backgroundColor, foregroundColor.rgb, appearance.foregroundOpacity);
    #else //#elif _BACKGROUND_MODE_COLORFUL
    half targetL = appearance.brightness / 2. + .5;
    backgroundColor = /*saturate*/(lerp(LinearRgbToLuminance(backgroundColor), backgroundColor, appearance.vibrancy));
    backgroundColor = /*saturate*/(backgroundColor + (targetL - backgroundColor) * appearance.flatten);
    backgroundColor = lerp(
        backgroundColor,
        setL(backgroundColor, LinearRgbToLuminance(backgroundColor), targetL),
        appearance.foregroundOpacity
    );
    color.rgb = gamutClip(backgroundColor * foregroundColor.rgb);
    #endif

    return color;
}
