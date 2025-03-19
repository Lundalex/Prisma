using UnityEngine;

public class PathDistanceSensorOverride : RigidBodySensorOverride
{
    private Main main;

    private Vector2 lastPos = Vector2.positiveInfinity;
    private float distanceTraveled = 0;

    public override void ValueOverride(ref float value, RBData rbData)
    {
        if (main == null) main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        float dir = rbData.vel_AsInt2.x > 0 ? 1 : -1;
        Vector2 pos = (Vector2)rbData.pos * main.SimUnitToMetersFactor;

        if (lastPos.x == float.PositiveInfinity) lastPos = pos;
        else
        {
            Vector2 dst = pos - lastPos;
            if (dst.magnitude > 0.001) distanceTraveled += dir * dst.magnitude;
            lastPos = pos;
        }

        value = distanceTraveled;
    }
}