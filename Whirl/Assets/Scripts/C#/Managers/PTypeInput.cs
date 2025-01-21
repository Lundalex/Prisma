using Resources2;
using UnityEngine;
using PM = ProgramManager;

public class PTypeInput : MonoBehaviour
{
    public bool DoGenerateNewParticles;
    public PTypeState[] particleTypeStates;

    public void OnValidate() => PM.Instance.doOnSettingsChanged = true;
    
    public PType[] GetParticleTypes()
    {
        PType[] particleTypes = new PType[particleTypeStates.Length * 3];
        for (int i = 0; i < particleTypeStates.Length; i++)
        {
            int baseIndex = 3 * i;
            particleTypes[baseIndex] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].solidState);
            particleTypes[baseIndex + 1] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].liquidState);
            particleTypes[baseIndex + 2] = ConvertTemperatePropertiesToCelcius(particleTypeStates[i].gasState);
        }

        return particleTypes;
    }

    PType ConvertTemperatePropertiesToCelcius(PType pType)
    {
        pType.freezeThreshold = Utils.CelsiusToKelvin(pType.freezeThreshold);
        pType.vaporizeThreshold = Utils.CelsiusToKelvin(pType.vaporizeThreshold);

        return pType;
    }
}