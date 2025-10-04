// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

using System;
using UnityEngine;

namespace LeTai.Paraform.Scaffold
{
[Serializable]
public struct ParaformConfig
{
    public static readonly ParaformConfig DEFAULT = new ParaformConfig {
        cornerCurvature = 1.5f,
        filletCurvature = 2.5f,
        edgeWidth       = 64,
        elevation       = 100,
        cornerRadii     = new Vector4(64, 64, 64, 64),
    };

    public event Action changed;


#if UNITY_EDITOR
    [SerializeField]
    internal bool isCornersLinked;
#endif

    [SerializeField]
    [Tooltip("Indiviual corner radii. You can drag the rounded corner symbols to fine tune each radius. Click the link button to keep all corners the same.")]
    internal Vector4 cornerRadii;

    public Vector4 CornerRadii
    {
        get => cornerRadii;
        set
        {
            if (cornerRadii != value)
            {
                cornerRadii = value;
                NotifyChanged();
            }
        }
    }

    [SerializeField]
    [Range(0, 6)]
    [Tooltip("0 is a flat diagonal corner. 1 is the perfect circle. Value > 1 increase curvature continuity target: 2 for G2 continuity, 3 for G3, and so on. Note that curvature continuity requires increased transition length, and is not guaranteed if the corner radius is too large compared to side length.")]
    internal float cornerCurvature;

    public float CornerCurvature
    {
        get => cornerCurvature;
        set
        {
            if (!Mathf.Approximately(cornerCurvature, value))
            {
                cornerCurvature = value;
                NotifyChanged();
            }
        }
    }

    [SerializeField]
    [Range(0, 6)]
    [Tooltip("0 is a flat diagonal corner. 1 is the perfect circle. Value > 1 increase curvature continuity target: 2 for G2 continuity, 3 for G3, and so on")]
    internal float filletCurvature;

    public float FilletCurvature
    {
        get => filletCurvature;
        set
        {
            if (!Mathf.Approximately(filletCurvature, value))
            {
                filletCurvature = value;
                NotifyChanged();
            }
        }
    }

    [SerializeField]
    [Min(0)]
    internal float edgeWidth;

    [Tooltip("Bevel width or thickness")]
    public float EdgeWidth
    {
        get => edgeWidth;
        set
        {
            if (!Mathf.Approximately(edgeWidth, value))
            {
                edgeWidth = value;
                NotifyChanged();
            }
        }
    }

    [SerializeField]
    [Range(0, 1000)]
    [Tooltip("Distance to the below surface, affecting refraction")]
    internal float elevation;

    public float Elevation
    {
        get => elevation;
        set
        {
            if (!Mathf.Approximately(elevation, value))
            {
                elevation = value;
                NotifyChanged();
            }
        }
    }

    public void NotifyChanged()
    {
        changed?.Invoke();
    }
}
}
