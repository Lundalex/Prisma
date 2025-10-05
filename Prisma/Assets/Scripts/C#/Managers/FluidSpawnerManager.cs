using UnityEngine;
using System.Collections.Generic;
using Resources2;

public class FluidSpawnerManager : MonoBehaviour
{
    // Public
    public FluidSpawner[] enabledFluidSpawners;

    // Private
    private Main main;
    private List<Timer> timers = new();

    public void StartScript(Main main) => this.main = main;

    public void UpdateScript()
    {
        for (int i = 0; i < enabledFluidSpawners.Length; i++)
        {
            FluidSpawner fluidSpawner = enabledFluidSpawners[i];

            if (i >= timers.Count) timers.Add(new(Func.MsToSeconds(fluidSpawner.msSpawnInterval), TimeType.Clamped, true));

            if (timers[i].Check())
            {
                main.SubmitParticlesToSimulation(fluidSpawner.GenerateParticles());
            }
        }
    }
}
