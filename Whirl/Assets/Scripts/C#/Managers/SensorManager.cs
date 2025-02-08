using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Resources2;

public class SensorManager : MonoBehaviour
{
    [Range(5.0f, 100.0f), SerializeField] public float msRigidBodyDataRetrievalInterval;
    [Range(5.0f, 100.0f), SerializeField] public float msFluidDataRetrievalInterval;
    [Range(5.0f, 500.0f)] public float msGraphPointSubmissionFrequency;
    [SerializeField] private bool doUpdateGraphsAsync = true;
    [Range(10.0f, 2000.0f), SerializeField] private float msGraphUpdateFrequency;

    // Retrieved data
    [NonSerialized] public RBData[] retrievedRBDatas;
    [NonSerialized] public RecordedFluidData[] retrievedFluidDatas;

    // Graph charts
    private List<GraphController> graphControllers = new();

    // References
    [NonSerialized] public List<SensorBase> sensors;
    private Main main;

    private bool programRunning;
    public void StartScript(Main main)
    {
        this.main = main;

        programRunning = true;
        StartCoroutine(RetrieveRigidBodyBufferDatasCoroutine());
        StartCoroutine(RetrieveParticleBufferDatasCoroutine());
        StartCoroutine(UpdateGraphsCoroutine());
    }

    public void SubscribeGraphToCoroutine(GraphController graphController) => graphControllers.Add(graphController);

    private IEnumerator RetrieveRigidBodyBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RBDataBuffer != null && sensors != null)
            {
                bool hasRigidBodySensor = sensors.OfType<RigidBodySensor>().Any() || sensors.OfType<RigidBodyArrow>().Any();
                if (hasRigidBodySensor)
                {
                    ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents => 
                    {
                        retrievedRBDatas = contents;
                        foreach (SensorBase sensor in sensors)
                        {
                            if (sensor is RigidBodySensor rigidBodySensor)
                            {
                                rigidBodySensor.UpdateSensor();
                            }
                            else if (sensor is RigidBodyArrow rigidBodyArrow)
                            {
                                rigidBodyArrow.UpdateSensor();
                            }
                        }
                    });
                }
            }

            yield return new WaitForSeconds(Func.MsToSeconds(msRigidBodyDataRetrievalInterval));
        }
    }

    private IEnumerator RetrieveParticleBufferDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (main.RecordedFluidDataBuffer != null && sensors != null)
            {
                bool hasFluidSensor = sensors.OfType<FluidSensor>().Any();
                if (hasFluidSensor)
                {
                    ComputeHelper.GetBufferContentsAsync<RecordedFluidData>(main.RecordedFluidDataBuffer, contents => 
                    {
                        retrievedFluidDatas = contents;
                        foreach (SensorBase sensor in sensors)
                        {
                            if (sensor is FluidSensor fluidSensor)
                            {
                                fluidSensor.UpdateSensor();
                            }
                        }
                    });
                }
            }

            yield return new WaitForSeconds(Func.MsToSeconds(msFluidDataRetrievalInterval));
        }
    }

    private IEnumerator UpdateGraphsCoroutine()
    {
        int graphCount = 0;
        while (programRunning)
        {
            if (graphControllers.Count > 0)
            {
                graphCount++;
                graphCount %= graphControllers.Count;

                graphControllers[graphCount].UpdateGraph();
            }
            
            float waitTimeSeconds;
            bool doInstantUpdate = !doUpdateGraphsAsync && graphCount != 0;
            if (doUpdateGraphsAsync) waitTimeSeconds = Func.MsToSeconds(graphControllers.Count > 0 ? (msGraphUpdateFrequency / graphControllers.Count) : 10.0f);
            else waitTimeSeconds = Func.MsToSeconds(doInstantUpdate ? 10.0f : msGraphUpdateFrequency);

            yield return new WaitForSeconds(waitTimeSeconds);
        }
    }

    private void OnDestroy() => programRunning = false;
}