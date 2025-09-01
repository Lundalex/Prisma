using System;
using Unity.Mathematics;
using UnityEngine;

[Serializable]
public struct MatInput
{
    public string name;

    [Header("Optional: source textures from a Unity Material")]
    public Material unityMaterial;  // If set, textures are extracted and ORM is auto-combined.

    // Micro-PBR textures (packed into the shared Atlas). Used if no material is provided,
    // or to override individual maps from the material if you assign them.
    public Texture2D colorTexture;   // Albedo
    public Texture2D normalTexture;  // Normal map (tangent-space)
    // public Texture2D occlusionTexture;     // Occlusion (R)
    // public Texture2D roughnessTexture;     // Roughness (G)
    // public Texture2D metalnessTexture;     // Metalness (B)
    public Texture2D ormTexture;     // Metalness (B)

    // UV transform / tiling
    public float colorTextureUpScaleFactor; // sign controls mirror repeat (handled in code)
    public float2 sampleOffset;

    // Material params
    public float opacity;
    public bool disableMirrorRepeat;
    public bool transparentEdges;
    public float3 baseColor;
    public float3 sampleColorMultiplier;
    public float3 edgeColor;
}