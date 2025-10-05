// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.Paraform.Scaffold;
using UnityEngine;

namespace LeTai.Paraform
{
public static class ParaformUtils
{
    public static (Vector4, float) NormalizeShape(Vector4 radii, Vector2 extent, float cornerCurvature, out float maxRadii)
    {
        float circleTipSpacing = Mathf.Sqrt(.5f) - 1f;

        maxRadii = Mathf.Min(extent.x, extent.y);

        float largest = Mathf.Max(Mathf.Max(Mathf.Max(radii.x, radii.y), radii.z), radii.w);

        float ratio     = largest / maxRadii;
        float termInLog = 1f + ratio * circleTipSpacing;
        float nCap      = Mathf.Max(2, -Mathf.Log(2f) / Mathf.Log(termInLog));

        float n = Mathf.Min(nCap, cornerCurvature + 1);

        var scale = n < 2f ? 1f : circleTipSpacing / (Mathf.Pow(2f, -1f / n) - 1f);
        return (
            new Vector4(Mathf.Min(maxRadii, radii.x * scale),
                        Mathf.Min(maxRadii, radii.y * scale),
                        Mathf.Min(maxRadii, radii.z * scale),
                        Mathf.Min(maxRadii, radii.w * scale))
          , n
        );
    }

    public static bool IsRaycastLocationValid(ParaformConfig config, Rect rect, Vector2 localPoint)
    {
        var d = DistanceToEdge(localPoint - rect.center, rect.size, config.CornerRadii, config.CornerCurvature);
        return d <= 0;
    }

    public static float DistanceToEdge(Vector2 localPosition, Vector2 size, Vector4 radii, float cornerCurvature)
    {
        var b = size / 2f;
        var (r, n) = NormalizeShape(radii, b, cornerCurvature, out _);

        r = new Vector4(
            (localPosition.x > 0) ? r.x : r.z,
            (localPosition.x > 0) ? r.y : r.w,
            r.z,
            r.w
        );
        var radius = (localPosition.y > 0) ? r.x : r.y;

        var q            = new Vector2(Mathf.Abs(localPosition.x), Mathf.Abs(localPosition.y)) - b + Vector2.one * radius;
        var q0           = Vector2.Max(q, Vector2.zero);
        var m            = Mathf.Pow(q0.x, n) + Mathf.Pow(q0.y, n);
        var q0norm       = Mathf.Pow(m, 1.0f / n);
        var edgeDistance = Mathf.Min(Mathf.Max(q.x, q.y), 0) + q0norm - radius;

        return edgeDistance;
    }
}
}
