// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

#include "../../TranslucentImage/Resources/Shaders/common.hlsl"

/// Output refraction offset in screenspace uv
/// Assume I = (0, 0, 1)
/// 0 >= normal.z >= -1
/// 0 < eta <= 1
inline half2 GetRefractedScreenOffset(half3 normal, half height, half eta)
{
    float dotNI = normal.z;
    float k = 1.0f - eta * eta * (1.0f - dotNI * dotNI);
    // half3 refracted = float3(0, 0, eta) - ((eta * dotNI + sqrtApprox01(k)) * normal); // not good enough at low ior and high elevation. branch?
    half3 refracted = float3(0, 0, eta) - ((eta * dotNI + sqrt(k)) * normal);

    half2 offset = refracted.xy * height / (refracted.z * _ScreenParams.xy);

    return offset;
}
