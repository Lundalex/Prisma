public class PathVelocitySensorOverride : RigidBodySensorOverride
{
    public override void ValueOverride(ref float value, RBData rbData)
    {
        // fart -> hastighet längs vägbana
        float dir = rbData.vel_AsInt2.x > 0 ? 1 : -1;
        value *= dir;
    }
}