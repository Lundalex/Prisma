public class LaserSensorOverride : RigidBodySensorOverride
{
    public float yThreshold;
    public float valueOverride;

    public override void ValueOverride(ref float value, RBData rbData)
    {
        if (rbData.pos.y > yThreshold)
        {
            value = valueOverride;
        }
    }
}