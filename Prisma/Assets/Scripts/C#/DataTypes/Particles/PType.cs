using System;
using UnityEngine;

/// <summary>
/// Authoring/CPU-side particle type. Uses direct CustomMat reference.
/// </summary>
[Serializable]
public struct PType
{
    [Header("Inter-Particle Springs")]
    public int   fluidSpringGroup;
    public float springPlasticity;
    public float springStiffness;
    public float springTolDeformation;

    [Header("Thermal Properties")]
    public float thermalConductivity;
    public float specificHeatCapacity;
    public float freezeThreshold;   // author in °C (converted to K at runtime)
    public float vaporizeThreshold; // author in °C (converted to K at runtime)

    [Header("Inter-particle Forces")]
    public float pressure;
    public float nearPressure;
    public float viscosity;
    public float gravity;

    [Header("Particle Properties")]
    public float mass;
    public float targetDensity;
    public float damping;
    public float passiveDamping;

    [Header("Material")]
    public CustomMat material;

    [Header("Simulation Engine")]
    public float influenceRadius;
}

/// <summary>
/// GPU-side layout (exact copy of the previous PType before this change).
/// Keeps matIndex since the GPU requires indices.
/// </summary>
[Serializable]
public struct PTypeData
{
    // Inter-Particle Springs
    public int   fluidSpringGroup;
    public float springPlasticity;
    public float springStiffness;
    public float springTolDeformation;

    // Thermal Properties
    public float thermalConductivity;
    public float specificHeatCapacity;
    public float freezeThreshold;
    public float vaporizeThreshold;

    // Inter-particle Forces
    public float pressure;
    public float nearPressure;
    public float viscosity;
    public float gravity;

    // Particle Properties
    public float mass;
    public float targetDensity;
    public float damping;
    public float passiveDamping;

    // Material (index in atlas/material buffer)
    public int   matIndex;

    // Simulation Engine
    public float influenceRadius;
}