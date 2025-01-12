using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct MatInput
{
    public string name;
    public Texture2D colorTexture;
    public float colorTextureUpScaleFactor;
    public float2 sampleOffset;
    public float opacity;
    public bool transparentEdges;
    public float3 baseColor;
    public float3 sampleColorMultiplier;
    public float3 edgeColor;
};