using System;
using Resources2;
using UnityEngine;

public class RigidBodySensor : Sensor
{
    [Header("Sensor Settings")]
    [SerializeField] private RigidBodySensorType rigidBodySensorType;
    [SerializeField] private bool doInterpolation;
    [Range(1.0f, 20.0f), SerializeField] private float moveSpeed;
    [NonSerialized] public int linkedRBIndex = -1;
    [NonSerialized] public bool firstDataRecieved = false;

    // Private
    private Vector2 currentTargetPosition;

    public override void InitSensor(Vector2 sensorUIPos)
    {
        sensorUI.rectTransform.localPosition = SimSpaceToCanvasSpace(positionType == PositionType.Relative ? sensorUIPos + localTargetPos : localTargetPos);
        firstDataRecieved = false;
    }

    public override void UpdatePosition()
    {
        if (firstDataRecieved)
        {
            Vector2 canvasTargetPosition = SimSpaceToCanvasSpace(currentTargetPosition);
            
            // Interpolate between the current position and the target position
            sensorUI.SetPosition((doInterpolation && positionType == PositionType.Relative) ? Vector2.Lerp(sensorUI.rectTransform.localPosition, canvasTargetPosition, Time.deltaTime * moveSpeed) : canvasTargetPosition);
        }
    }

    public override void UpdateSensor()
    {
        if (sensorUI != null)
        {
            if (linkedRBIndex == -1) Debug.LogWarning("Sensor not linked to any rigid body; It will not be updated. RigidBodySensor: " + this.name);
            else
            {
                RBData[] retrievedRBDatas = sensorManager.retrievedRBDatas;

                RBData rbData = retrievedRBDatas[linkedRBIndex];
                lastJointPos = (Vector2)rbData.pos;

                // Init sensor UI position
                if (!firstDataRecieved)
                {
                    currentTargetPosition = localTargetPos;
                    if (positionType == PositionType.Relative) currentTargetPosition += lastJointPos;
                    sensorUI.SetPosition(SimSpaceToCanvasSpace(currentTargetPosition));
                    firstDataRecieved = true;
                }
                else currentTargetPosition = positionType == PositionType.Relative ? lastJointPos + localTargetPos : localTargetPos;

                UpdateSensorContents(retrievedRBDatas, linkedRBIndex);
            }
        }
    }

    private void UpdateSensorContents(RBData[] rBDatas, int linkedRBIndex)
    {
        float simUnitToMetersFactor = main.SimUnitToMetersFactor;

        RBData rbData = rBDatas[linkedRBIndex];

        Vector2 vel = Func.Int2ToFloat2(rbData.vel_AsInt2, main.FloatIntPrecisionRB) * simUnitToMetersFactor;
        Vector2 pos = (Vector2)rbData.pos * simUnitToMetersFactor;

        float value = 0;
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                value = rbData.mass;
                break;

            case RigidBodySensorType.Velocity:
                value = vel.magnitude;
                break;

            case RigidBodySensorType.Velocity_X:
                value = vel.x;
                break;

            case RigidBodySensorType.Velocity_Y:
                value = vel.y;
                break;

            case RigidBodySensorType.RotationalVelocity:
                value = Func.IntToFloat(rbData.rotVel_AsInt, main.FloatIntPrecisionRB);
                break;

            case RigidBodySensorType.Position_X:
                value = pos.x;
                break;

            case RigidBodySensorType.Position_Y:
                value = pos.y;
                break;

            case RigidBodySensorType.FrictionForce:
                value = ((Vector2)rbData.recordedFrictionForce).magnitude;
                break;

            case RigidBodySensorType.SpringForce:
                value = ((Vector2)rbData.recordedSpringForce).magnitude;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        if (Mathf.Abs(value * Mathf.Pow(10, 3 * (3 - minPrefixIndex))) < minDisplayValue) value = 0.0f;
        
        value *= valueMultiplier;
        value += valueOffset;

        (string prefix, float displayValue) = GetMagnitudePrefix(value, minPrefixIndex);
        bool isNewUnit = SetSensorUnit(prefix);

        sensorUI.SetMeasurement(displayValue, numDecimals, isNewUnit);
        AddSensorDataToGraph(value);
    }

    // Returns whether the unit is different from last frame
    public override bool SetSensorUnit(string prefix = "")
    {
        string baseUnit = "";
        if (doUseCustomUnit) baseUnit = customUnit;
        else switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                baseUnit = "g";
                break;

            case RigidBodySensorType.Velocity:
            case RigidBodySensorType.Velocity_X:
            case RigidBodySensorType.Velocity_Y:
                baseUnit = "m/s";
                break;

            case RigidBodySensorType.RotationalVelocity:
                baseUnit = "r/s";
                break;

            case RigidBodySensorType.Position_X:
            case RigidBodySensorType.Position_Y:
                baseUnit = "m";
                break;

            case RigidBodySensorType.FrictionForce:
            case RigidBodySensorType.SpringForce:
                baseUnit = "N";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        string unit = prefix + baseUnit;

        // If the new baseUnit differs from the previous baseUnit, update the sensor baseUnit
        if (unit != lastUnit)
        {
            sensorUI.SetUnit(baseUnit, unit);
            lastUnit = unit;
            return true;
        }

        return false;
    }

    public override void SetSensorTitle()
    {
        string title = "Titel här";
        if (doUseCustomTitle) title = customTitle;
        else switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                title = "Massa";
                break;

            case RigidBodySensorType.Velocity:
                title = "Fart";
                break;

            case RigidBodySensorType.Velocity_X:
                title = "Hastighet X";
                break;

            case RigidBodySensorType.Velocity_Y:
                title = "Hastighet Y";
                break;

            case RigidBodySensorType.RotationalVelocity:
                title = "Vinkelhastighet";
                break;

            case RigidBodySensorType.Position_X:
                title = "Position X";
                break;

            case RigidBodySensorType.Position_Y:
                title = "Position Y";
                break;

            case RigidBodySensorType.FrictionForce:
                title = "Friktion";
                break;

            case RigidBodySensorType.SpringForce:
                title = "Dragkraft";
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.SetTitle(title);
    }

    public override void UpdateSensorTypeDropdown()
    {
        int itemIndex = 0;
        switch (rigidBodySensorType)
        {
            case RigidBodySensorType.Mass:
                itemIndex = 0;
                break;

            case RigidBodySensorType.Velocity:
                itemIndex = 1;
                break;

            case RigidBodySensorType.Velocity_X:
                itemIndex = 2;
                break;

            case RigidBodySensorType.Velocity_Y:
                itemIndex = 3;
                break;

            case RigidBodySensorType.RotationalVelocity:
                itemIndex = 4;
                break;

            case RigidBodySensorType.Position_X:
                itemIndex = 5;
                break;

            case RigidBodySensorType.Position_Y:
                itemIndex = 6;
                break;

            case RigidBodySensorType.FrictionForce:
                itemIndex = 7;
                break;

            case RigidBodySensorType.SpringForce:
                itemIndex = 8;
                break;

            default:
                Debug.LogWarning("Unrecognised RigidBodySensorType: " + this.name);
                break;
        }

        sensorUI.rigidBodySensorTypeSelect.selectedItemIndex = itemIndex;
    }

    public void SetRigidBodySensorType(RigidBodySensorType rigidBodySensorType)
    {
        this.rigidBodySensorType = rigidBodySensorType;
        SetSensorTitle();
    }
}