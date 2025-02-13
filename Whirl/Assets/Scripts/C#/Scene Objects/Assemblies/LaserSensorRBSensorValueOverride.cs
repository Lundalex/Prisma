using UnityEngine;

public class LaserSensorOverride : MonoBehaviour
{
    [SerializeField] private RigidBodySensor mySensor;
    public float yThreshold;
    public float valueOverride;

    void OnEnable() => mySensor.CustomValueOverride = ValueOverride;

    public void ValueOverride(ref float value, RBData rbData)
    {
        if (rbData.pos.y > yThreshold)
        {
            value = valueOverride;
        }
    }
}