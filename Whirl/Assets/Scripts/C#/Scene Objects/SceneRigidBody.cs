using System;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using UnityEngine;
using PM = ProgramManager;

#if UNITY_EDITOR
    using UnityEditor;
#endif

[RequireComponent(typeof(PolygonCollider2D)), ExecuteAlways]
public class SceneRigidBody : Polygon
{
    public bool doCenterPosition = false;
    public bool doDrawBody = true;
    public float editorLineAnimationSpeed = 10;
    
    [Header("Simulation Object Settings")]
    public bool doOverrideGridSpacing = false;
    [Range(0.1f, 10.0f)] public float defaultGridSpacing = 0.5f;
    public bool addInBetweenPoints = true;
    public bool doRecursiveSubdivisison = false;
    [Range(0.5f, 10.0f)] public float minDstForSubDivision = 0;
    public Sensor[] linkedSensors;
    public RBInput rbInput;

    [Header("Approximated Starting Values")]
    public string approximatedVolume;
    public string approximatedSpringLength;
    public string approximatedSpringForce;

    // NonSerialized
    [NonSerialized] public Vector2[] points;
    [NonSerialized] public Vector2 lastPosition = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cachedCentroid = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cachedRelativeLinkPos;
    [NonSerialized] public Vector2 cachedLinearMotorOffset;
    [NonSerialized] public ConstraintType lastLinkType;
    [NonSerialized] public Vector2 lastLocalLinkPosThisRB;
    [NonSerialized] public Vector2 lastLocalLinkPosOtherRB;
    [NonSerialized] public bool lastLinkTypeSet = false;

#if UNITY_EDITOR
    // Editor
    private int frameCount = 0;
    private int framesSinceLastPositionChange = 0;
    private Vector2 lastFramePosition = Vector2.zero;
    private float lastFrameLerpTimeOffset = 0;
    private Vector2 lastStartPos = Vector2.zero;
    private Vector2 lastEndPos = Vector2.zero;
    private bool lastOverrideCentroid = false;
    private bool lastOverrideCentroidSet = false;

    public override void OnEditorUpdate()
    {
        if (Application.isPlaying || this == null) return;

        // Skip re‑assigning collider points if user is actively dragging handles
        bool userIsModifying = Tools.current == Tool.Move || Tools.current == Tool.Rotate || Tools.current == Tool.Scale;
        if (userIsModifying) return;

        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();

        // Update linear motor offset
        float newLerpTimeOffset = rbInput.lerpTimeOffset;
        Vector2 newStartPos = rbInput.startPos;
        Vector2 newEndPos = rbInput.endPos;
        if (newLerpTimeOffset != lastFrameLerpTimeOffset || newStartPos != lastStartPos || newEndPos != lastEndPos)
        {
            lastFrameLerpTimeOffset = newLerpTimeOffset;
            lastStartPos = newStartPos;
            lastEndPos = newEndPos;
            cachedLinearMotorOffset = GetLinearMotorOffset();
            transform.localPosition = rbInput.startPos + cachedLinearMotorOffset;
        }

        if (!lastOverrideCentroidSet)
        {
            lastOverrideCentroid = rbInput.overrideCentroid;
            lastOverrideCentroidSet = true;
        }

        if (rbInput.overrideCentroid != lastOverrideCentroid)
        {
            lastOverrideCentroid = rbInput.overrideCentroid;
            rbInput.overrideCentroidPosition = transform.position;
        }

        // Track transform changes
        if (lastFramePosition != (Vector2)transform.localPosition)
        {
            lastFramePosition = transform.localPosition;
            framesSinceLastPositionChange = 0;
        }
        else framesSinceLastPositionChange++;

        if (framesSinceLastPositionChange < 20) return; // wait a bit if user is dragging?

        // Center if requested
        if (doCenterPosition)
        {
            CenterPolygonPosition();
            doCenterPosition = false;
        }

        bool forceUpdateCachedData = frameCount++ % 10 == 0;
        if ((lastPosition - (Vector2)transform.localPosition).sqrMagnitude > 10.0f ||
            (lastLocalLinkPosThisRB - rbInput.localLinkPosThisRB).sqrMagnitude > 0.01f ||
            (lastLocalLinkPosOtherRB - rbInput.localLinkPosOtherRB).sqrMagnitude > 0.01f ||
            forceUpdateCachedData)
        {
            UpdateCachedData();
        }

        // Check if link type changed
        if (!lastLinkTypeSet)
        {
            lastLinkType = rbInput.constraintType;
            lastLinkTypeSet = true;
        }
        if (lastLinkType != rbInput.constraintType)
        {
            lastLinkType = rbInput.constraintType;
            CenterPolygonPosition();
        }

        if (snapPointToGrid) SnapPointsToGrid();
    }

    private void UpdateCachedData()
    {
        cachedCentroid = ComputeCentroid(defaultGridSpacing);

        // If rigid and linked, try to position accordingly
        if (rbInput.constraintType == ConstraintType.Rigid)
        {
            if (rbInput.linkedRigidBody == null)
            {
                Debug.LogWarning("Linked rigid body not set. SceneRigidBody: " + name);
                return;
            }

            Vector2 thisCentroid = cachedCentroid;
            Vector2 otherCentroid = rbInput.linkedRigidBody.cachedCentroid;
            Vector2 thisCentroidRelative = thisCentroid - lastPosition;
            Vector2 localLinkPosOther = rbInput.localLinkPosOtherRB;
            Vector2 localLinkPosThis = rbInput.localLinkPosThisRB;
            
            Vector2 newPos = otherCentroid - thisCentroidRelative + localLinkPosOther - localLinkPosThis;
            bool doUpdatePosition = (newPos.x < float.MaxValue) &&
                                    (newPos.y < float.MaxValue) &&
                                    ((lastPosition - newPos).sqrMagnitude > 10.0f);

            if (doUpdatePosition)
            {
                transform.localPosition = newPos;
                cachedRelativeLinkPos = thisCentroid + localLinkPosThis;
            }
        }
        else if (rbInput.constraintType == ConstraintType.LinearMotor)
        {
            // If linear motor, set position from offset
            transform.localPosition = rbInput.startPos + cachedLinearMotorOffset;
        }

        if ((lastPosition - (Vector2)transform.localPosition).sqrMagnitude > 10.0f)
        {
            lastPosition = transform.localPosition;
        }
        lastLocalLinkPosThisRB = rbInput.localLinkPosThisRB;
        lastLocalLinkPosOtherRB = rbInput.localLinkPosOtherRB;
    }

    public override void SnapPointsToGrid()
    {
        base.SnapPointsToGrid();

        if (!rbInput.overrideCentroid) CenterPolygonPosition();
    }
#endif

    public Vector2[] GeneratePoints(float gridSpacing, Vector2 offset)
    {
        if (gridSpacing == 0 || doOverrideGridSpacing) gridSpacing = defaultGridSpacing;
        if (!Application.isPlaying) gridSpacing *= 2;
        SetPolygonData();

        List<Vector2> generatedPoints = new();

        Vector2 min = Func.MinVector2(Edges.Select(edge => Func.MinVector2(edge.start, edge.end)).ToArray());
        Vector2 max = Func.MaxVector2(Edges.Select(edge => Func.MaxVector2(edge.start, edge.end)).ToArray());

        int safetyCounter = 0;
        for (float x = min.x; x <= max.x; x += gridSpacing)
        {
            for (float y = min.y; y <= max.y; y += gridSpacing)
            {
                if (safetyCounter++ > 1000000)
                {
                    Debug.LogError("SceneRigidBody point generation took too many iterations to complete. Make sure the polygon geometry is set correctly, or increase the grid spacing override setting. SceneRigidBody: " + this.name);
                    return generatedPoints.ToArray();
                }
                Vector2 point = new Vector2(x, y) + offset;
                if (IsPointInsidePolygon(point))
                {
                    generatedPoints.Add(point);
                }
            }
        }
        return generatedPoints.ToArray();
    }

    public Vector2 ComputeCentroid(float gridSpacing)
    {
        // Possibly override with a user-specified centroid
        (bool hasAltCentroid, Vector2 altCentroid) = GetAlternativeCentroid();
        if (hasAltCentroid) return altCentroid;

        // Otherwise, generate grid points and average them for an accurate centroid
        Vector2[] points = GeneratePoints(gridSpacing, Vector2.zero);
        int numPoints = points.Length;
        if (numPoints == 0) return Vector2.zero;

        // Update the approximated volume in the inspector
        ApproximateVolume(numPoints, gridSpacing);

        // Calculate centroid
        Vector2 centroid = Vector2.zero;
        foreach (Vector2 point in points) centroid += point;
        centroid /= numPoints;

        cachedCentroid = centroid;
        return centroid;
    }

    public (float, float) ComputeInertiaAndBalanceRigidBody(
        ref Vector2[] vectors,
        ref Vector2 rigidBodyPosition,
        Vector2 offset,
        float? gridDensityInput = null
    ) {
        float gridSpacing = gridDensityInput ?? 0.2f;

        // Points from the edges fill, for inertia
        Vector2[] points = GeneratePoints(gridSpacing, offset);
        int numPoints = points.Length;
        if (numPoints == 0) return (0f, 0f);

        // Update the approximated volume in the inspector
        ApproximateVolume(numPoints, gridSpacing);

        float pointMass = rbInput.mass / numPoints;

        (bool hasAltCentroid, Vector2 altCentroid) = GetAlternativeCentroid();
        Vector2 centroid;
        if (hasAltCentroid)
        {
            cachedCentroid = altCentroid;
            centroid = altCentroid;
        }
        else
        {
            centroid = Vector2.zero;
            foreach (Vector2 pt in points) centroid += pt;
            centroid /= numPoints;
            cachedCentroid = centroid;
        }

        // Shift the vectors array to be centered around (0, 0)
        Vector2 shift = rigidBodyPosition - centroid;
        for (int i = 0; i < vectors.Length; i++)
        {
            vectors[i] += shift;
        }
        rigidBodyPosition = centroid;

        // Sum distance^2 from centroid for inertia
        float inertia = 0.0f;
        foreach (Vector2 pt in points)
        {
            float dstSqr = (pt - rigidBodyPosition).sqrMagnitude;
            inertia += dstSqr;
        }
        inertia *= pointMass;

        // Max radius squared, which will be used for early collision solver exits
        float maxRadiusSqr = 0.0f;
        foreach (Vector2 vec in vectors)
        {
            // Make sure the new path flag doesn't ruin the calculations
            Vector2 validatedVec = vec;
            if (validatedVec.x > 50000) validatedVec.x -= 100000;

            maxRadiusSqr = Mathf.Max(maxRadiusSqr, validatedVec.sqrMagnitude);
        }
        maxRadiusSqr += 1.0f; // small offset to render edges properly

        return (inertia, maxRadiusSqr);
    }

    private void ApproximateVolume(int numPoints, float gridSpacing)
    {
        if (!Application.isPlaying) gridSpacing *= 2;

        if (PM.Instance.main == null) PM.Instance.main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        // Z-depth = 1
        float approxVolume = numPoints * Func.Sqr(gridSpacing * PM.Instance.main.SimUnitToMetersFactor) * PM.Instance.main.ZDepthMeters * 1000; // *1000: m^3 -> dm^3 = l
        approximatedVolume = approxVolume.ToString() + " l";
    }

    private (bool hasAltCentroid, Vector2 altCentroid) GetAlternativeCentroid()
    {
        if (rbInput.overrideCentroid)
        {
            cachedCentroid = rbInput.overrideCentroidPosition
                + (Vector2)transform.position
                - (Vector2)transform.localPosition;
            return (true, cachedCentroid);
        }
        else if (rbInput.constraintType == ConstraintType.LinearMotor)
        {
            cachedLinearMotorOffset = GetLinearMotorOffset();
            transform.localPosition = rbInput.startPos + cachedLinearMotorOffset;

            cachedCentroid = rbInput.startPos
                             + cachedLinearMotorOffset
                             + (Vector2)transform.position
                             - (Vector2)transform.localPosition;
            return (true, cachedCentroid);
        }
        return (false, Vector2.positiveInfinity);
    }

    public Vector2 GetLinearMotorOffset()
    {
        if (rbInput.constraintType == ConstraintType.LinearMotor)
        {
            float t;
            if (rbInput.doRoundTrip)
            {
                t = (Mathf.Sin((rbInput.lerpTimeOffset + 0.75f) * Mathf.PI * 2.0f) + 1.0f) * 0.5f;
            }
            else
            {
                t = rbInput.lerpTimeOffset % 1.0f;
            }
            return Func.LerpVector2(rbInput.startPos, rbInput.endPos, t) - rbInput.startPos;
        }
        return Vector2.zero;
    }
}
