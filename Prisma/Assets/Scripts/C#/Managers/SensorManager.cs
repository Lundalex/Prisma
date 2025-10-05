using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Resources2;

public class SensorManager : MonoBehaviour
{
    [Range(5.0f, 100.0f)] public float msRigidBodyDataRetrievalInterval;
    [Range(5.0f, 100.0f)] public float msFluidDataRetrievalInterval;
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

    // Private
    private bool programRunning;
    private bool rigidBodyUpdateRequested;
    private bool fluidUpdateRequested;

    public void StartScript(Main main)
    {
        this.main = main;

        programRunning = true;
        RequestUpdate();
        StartCoroutine(RetrieveRigidBodyDatasCoroutine());
        StartCoroutine(RetrieveFluidDatasCoroutine());
        StartCoroutine(UpdateGraphsCoroutine());
    }

    public void Stop()
    {
        programRunning = false;
        StopAllCoroutines();
        retrievedRBDatas = null;
        retrievedFluidDatas = null;
    }

    public void SoftReset(Main main)
    {
        Stop();
        StartScript(main);
    }


    public void SubscribeGraphToCoroutine(GraphController graphController) => graphControllers.Add(graphController);

    private IEnumerator RetrieveRigidBodyDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve rigid body data buffer asynchronously
            if (rigidBodyUpdateRequested && main.RBDataBuffer != null && sensors != null)
            {
                rigidBodyUpdateRequested = false;
                bool hasRigidBodySensor = sensors.OfType<RigidBodySensor>().Any() || sensors.OfType<RigidBodyArrow>().Any();
                if (hasRigidBodySensor)
                {
                    ComputeHelper.GetBufferContentsAsync<RBData>(main.RBDataBuffer, contents => 
                    {
                        if (programRunning)
                        {
                            retrievedRBDatas = contents;
                            foreach (SensorBase sensor in sensors)
                            {
                                if (sensor is RigidBodySensor || sensor is RigidBodyArrow)
                                {
                                    sensor.UpdateSensor();
                                }
                            }
                        }
                    });
                }
            }

            yield return new WaitForSeconds(Func.MsToSeconds(msRigidBodyDataRetrievalInterval));
        }
    }

    private IEnumerator RetrieveFluidDatasCoroutine()
    {
        while (programRunning)
        {
            // Retrieve fluid buffer data asynchronously
            if (fluidUpdateRequested && main.RecordedFluidDataBuffer != null && sensors != null)
            {
                fluidUpdateRequested = false;
                bool hasFluidSensor = sensors.OfType<FluidSensor>().Any();
                bool hasFluidArrowField = sensors.OfType<FluidArrowField>().Any();
                if (hasFluidSensor || hasFluidArrowField)
                {
                    ComputeHelper.GetBufferContentsAsync<RecordedFluidData>(main.RecordedFluidDataBuffer, contents => 
                    {
                        if (programRunning)
                        {
                            retrievedFluidDatas = contents;
                            foreach (SensorBase sensor in sensors)
                            {
                                if (sensor is FluidSensor || sensor is FluidArrowField)
                                {
                                    sensor.UpdateSensor();
                                }
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

    public void RequestUpdate() => rigidBodyUpdateRequested = fluidUpdateRequested = true;

    private void OnDestroy() => programRunning = false;
}