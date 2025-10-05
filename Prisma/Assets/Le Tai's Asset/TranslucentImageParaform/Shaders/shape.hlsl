// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

float L_n_norm(float2 v, float n)
{
    float2 ls = float2(max(v.x, v.y), min(v.x, v.y));
    if (ls.x == 0.0) // Only return if both are 0
        return 0.0;

    float2 logPowN = log2(ls) * n;
    half   logExpSum = logPowN.x + log2(1 + exp2(logPowN.y - logPowN.x));
    return exp2(logExpSum / n);
}

void roundedBox(float2 p, float2 b, float4 radii, float n, out float edgeDistance, out float2 gradient)
{
    radii.xy = (p.x > 0) ? radii.xy : radii.zw;
    radii.x = (p.y > 0) ? radii.x : radii.y;
    float radius = radii.x;

    float2 q = abs(p) - b + radius;
    float2 q0 = max(q, 0);
    float  q0norm = L_n_norm(q0, n);
    // q0norm = pow(pow(q0.x, n) + pow(q0.y, n), 1.0 / n);
    edgeDistance = min(max(q.x, q.y), 0) + q0norm - radius;

    float2 dq_dp = sign(p);
    float2 dq0norm_dq = q0norm > 0 ? pow(q0 / q0norm, n - 1) : 0;
    float2 dd_dq = float2(
        q.x >= q.y && q.x < 0 ? 1 : 0,
        q.x < q.y && q.y < 0 ? 1 : 0
    ) + dq0norm_dq;
    float2 dd_dp = dd_dq * dq_dp;

    gradient = dd_dp;
}

/// n > 0
void edgeParams(float edgeFactor, float n, out float y, out float2 normal)
{
    float base = 1.0 - pow(edgeFactor, n);
    float nRcp = 1.0 / n;

    y = pow(base, nRcp);

    normal.x = pow(edgeFactor, n - 1.0);
    normal.y = pow(base, 1.0 - nRcp);
    normal = normalize(normal);
}
