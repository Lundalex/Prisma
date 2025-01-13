using System;
using UnityEngine;

[Serializable]
public struct PType
{
    [Header("Inter-Particle Springs")]
    public int fluidSpringGroup;
    public float springPlasticity;
    public float springStiffness;
    public float springTolDeformation;

    [Header("Thermal Properties")]
    public float thermalConductivity;
    public float specificHeatCapacity;
    public float freezeThreshold;
    public float vaporizeThreshold;

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
    public int matIndex;
    
    [Header("Simulation Engine")]
    public float influenceRadius;
};