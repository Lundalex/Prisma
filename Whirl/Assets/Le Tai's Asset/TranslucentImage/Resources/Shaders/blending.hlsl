// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

#include <UnityCG.cginc>

half3 blendOverlay(half3 a, half3 b)
{
    return a < .5 ? 2 * a * b : 1 - 2 * (1 - a) * (1 - b);
}

half3 blendScreen(half3 a, half3 b)
{
    return 1 - (1 - a) * (1 - b);
}

half3 setL(half3 a, half lumA, half lumB)
{
    return a + (lumB - lumA);
}

half3 gamutClip(half3 color)
{
    half L = LinearRgbToLuminance(color);
    half minComp = min(min(color.r, color.g), color.b);
    half maxComp = max(max(color.r, color.g), color.b);

    // branches are never entered when the denom is 0

    // if (minComp < 0)
    //     // color = L + (color - L) * L / (L - minComp);
    //     result = L * (result - minComp) / (L - minComp);
    // if (maxComp > 1)
    //     result = L + (result - L) * (1 - L) / (maxComp - L);


    // Assumption: under blurring, branches are more likely to be coherence than not

    // fully scalar
    half scale = 1;
    if (minComp < 0)
        scale = L / (L - minComp);
    if (maxComp > 1)
        scale = min(scale, (1 - L) / (maxComp - L));

    // skipping the vector path should be worth the branch
    // if (scale < 1)
    if (scale < 1)
        color = L + (color - L) * scale;

    return color;
}
