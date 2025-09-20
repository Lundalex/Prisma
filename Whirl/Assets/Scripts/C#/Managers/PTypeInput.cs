using Resources2;
using UnityEngine;
using PM = ProgramManager;
using System.Collections.Generic;

public class PTypeInput : MonoBehaviour
{
    public PTypeState[] particleTypeStates;

    void OnValidate() => PM.Instance.doOnSettingsChanged = true;

    /// <summary>
    /// CPU-side types with CustomMat references left intact (thresholds converted to Kelvin).
    /// Useful if you need to inspect types on CPU.
    /// </summary>
    public PType[] GetParticleTypes()
    {
        PType[] particleTypes = new PType[particleTypeStates.Length * 3];
        for (int i = 0; i < particleTypeStates.Length; i++)
        {
            int baseIndex = 3 * i;
            particleTypes[baseIndex]     = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].solidState);
            particleTypes[baseIndex + 1] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].liquidState);
            particleTypes[baseIndex + 2] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].gasState);
        }

        return particleTypes;
    }

    /// <summary>
    /// GPU-side types (exact layout of old PType with int matIndex).
    /// Converts CustomMat references to indices using the provided mapping.
    /// </summary>
    public PTypeData[] GetParticleTypesData(Dictionary<CustomMat, int> matIndexMap)
    {
        PType[] cpuTypes = GetParticleTypes();
        PTypeData[] gpuTypes = new PTypeData[cpuTypes.Length];

        for (int i = 0; i < cpuTypes.Length; i++)
        {
            var p = cpuTypes[i];
            int matIndex = -1;
            if (p.material != null && matIndexMap != null)
            {
                if (!matIndexMap.TryGetValue(p.material, out matIndex)) matIndex = -1;
            }

            gpuTypes[i] = new PTypeData
            {
                // Inter-Particle Springs
                fluidSpringGroup   = p.fluidSpringGroup,
                springPlasticity   = p.springPlasticity,
                springStiffness    = p.springStiffness,
                springTolDeformation = p.springTolDeformation,

                // Thermal Properties
                thermalConductivity  = p.thermalConductivity,
                specificHeatCapacity = p.specificHeatCapacity,
                freezeThreshold      = p.freezeThreshold,
                vaporizeThreshold    = p.vaporizeThreshold,

                // Inter-particle Forces
                pressure     = p.pressure,
                nearPressure = p.nearPressure,
                viscosity    = p.viscosity,
                gravity      = p.gravity,

                // Particle Properties
                mass           = p.mass,
                targetDensity  = p.targetDensity,
                damping        = p.damping,
                passiveDamping = p.passiveDamping,

                // Material (GPU wants index)
                matIndex = matIndex,

                // Simulation
                influenceRadius = p.influenceRadius
            };
        }

        return gpuTypes;
    }

    private PType ConvertTemperatePropertiesToCelcius(PType pType)
    {
        pType.freezeThreshold = Utils.CelsiusToKelvin(pType.freezeThreshold);
        pType.vaporizeThreshold = Utils.CelsiusToKelvin(pType.vaporizeThreshold);
        return pType;
    }
}