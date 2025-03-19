using UnityEngine;

public abstract class RigidBodySensorOverride : MonoBehaviour
{
    [SerializeField] private RigidBodySensor sensor;

    void OnEnable() => sensor.CustomValueOverride = ValueOverride;

    public abstract void ValueOverride(ref float value, RBData rbData);
}