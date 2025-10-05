// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using LeTai.Common;
using LeTai.Paraform.Scaffold;
using UnityEngine;

namespace LeTai.Paraform
{
public readonly struct ParaformVertexDataEncoder
{
    readonly ParaformConfig _config;
    readonly Vector2        _extent;
    readonly Vector2        _origin;
    readonly float          _maxRadii;
    readonly Vector4        _effectiveRadii;
    readonly float          _effectiveCornerPower;
    readonly float          _effectiveEdgeWidth;
    readonly float          _scale;

    public ParaformVertexDataEncoder(RectTransform rt, ParaformConfig config)
    {
        _config = config;

        var rect = rt.rect;
        _extent = rect.size / 2f;
        _origin = rect.min + _extent;

        (_effectiveRadii, _effectiveCornerPower) = ParaformUtils.NormalizeShape(config.CornerRadii,
                                                                                _extent,
                                                                                config.CornerCurvature,
                                                                                out _maxRadii);

        _effectiveEdgeWidth = Mathf.Min(config.EdgeWidth,
                                        _effectiveRadii.x,
                                        _effectiveRadii.y,
                                        _effectiveRadii.z,
                                        _effectiveRadii.w);
        _effectiveEdgeWidth = Mathf.Max(1e-6f, _effectiveEdgeWidth);


        var localScale = rt.localScale;
        // var scale      = (localScale.x + localScale.y) / 2f;
        _scale = Mathf.Max(localScale.x, localScale.y);
    }

    public void WriteCommon(ref SpanWriter<float> writer)
    {
        // 1st vec
        writer.Write(Packing.FloatPacker.Uniform(10)
                            .Enqueue(_effectiveRadii.x, _maxRadii)
                            .Enqueue(_effectiveRadii.y, _maxRadii)
                            .Enqueue(_effectiveRadii.z, _maxRadii));
        writer.Write(Packing.FloatPacker.Uniform(10)
                            .Enqueue(_effectiveRadii.w,           _maxRadii)
                            .Enqueue(_effectiveCornerPower,       1.001f, 8)
                            .Enqueue(_config.FilletCurvature + 1, 1.001f, 8));
        writer.Write(Packing.FloatPacker.Uniform(15)
                            .Enqueue(_effectiveEdgeWidth, _maxRadii)
                            .Enqueue(_config.Elevation,   1000));
        // writer.Write(_scale);
        writer.Write(0);

        // 2nd vec.xy
        writer.Write(_extent.x);
        writer.Write(_extent.y);
    }

    public void WritePerVertex(ref SpanWriter<float> writer, Vector2 vertexPosition)
    {
        // 2nd vec.zw
        writer.Write(vertexPosition.x - _origin.x);
        writer.Write(vertexPosition.y - _origin.y);
    }
}
}
