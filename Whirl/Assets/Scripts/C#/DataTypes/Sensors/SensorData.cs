using UnityEngine;

public class SensorData
{
    public Sensor sensor;
    public SensorUI sensorUI;
    public GameObject sensorUIObject;
    public bool isSettingsViewActive;

    public SensorData(Sensor sensor, SensorUI sensorUI, GameObject sensorUIObject, bool isSettingsViewActive)
    {
        this.sensor = sensor;
        this.sensorUI = sensorUI;
        this.sensorUIObject = sensorUIObject;
        this.isSettingsViewActive = isSettingsViewActive;
    }
}