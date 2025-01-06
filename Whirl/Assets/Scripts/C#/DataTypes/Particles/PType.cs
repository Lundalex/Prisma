using System;
using UnityEngine;

[Serializable]
public struct PType
{
    [Header("Inter-Particle Springs")]
    public int fluidSpringGroup;
    public float springPlasticity;
    public float springTolDeformation;
    public float springStiffness;

    [Header("Thermals")]
    public float thermalConductivity;
    public float specificHeatCapacity;
    public float freezeThreshold;
    public float vaporizeThreshold;

    [Header("Pressure")]
    public float pressure;
    public float nearPressure;

    [Header("Runtime Properties")]
    public float mass;
    public float targetDensity;
    public float damping;
    public float passiveDamping;
    public float viscosity;
    public float gravity;

    [Header("Material")]
    public int matIndex;
    
    [Header("Simulation Engine")]
    public float influenceRadius;
};