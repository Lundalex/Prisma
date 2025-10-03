using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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
    public SensorBase[] linkedSensors;
    public RBInput rbInput;

    [Header("Material")]
    public CustomMat material;
    public CustomMat springMaterial;

    [Header("Approximated Starting Values")]
    public string approximatedVolume;
    public string approximatedSpringLength;
    public string approximatedSpringForce;

    // NonSerialized
    [NonSerialized] public Vector2 lastPosition = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cachedCentroid = Vector2.positiveInfinity;
    [NonSerialized] public Vector2 cachedRelativeLinkPos;
    [NonSerialized] public Vector2 cachedLinearMotorOffset;
    [NonSerialized] public ConstraintType lastLinkType;
    [NonSerialized] public Vector2 lastLocalLinkPosThisRB;
    [NonSerialized] public Vector2 lastLocalLinkPosOtherRB;
    [NonSerialized] public bool lastLinkTypeSet = false;

    // ===== Tiny mass-props cache (in-memory + PlayerPrefs in builds) =====
    private struct RBCalcCache
    {
        public int shapeHash;
        public float gridSpacing;
        public Vector2 centroidWorld;
        public int numPoints;
        public double sumR2AroundCentroid; // Σ |p - centroid|^2
    }

    private static readonly Dictionary<int, RBCalcCache> _memCache = new();
    private static readonly object _memCacheLock = new();

    private static string PrefKey(int shapeHash, float gridSpacing)
        => $"RBCACHE:{shapeHash}:{Mathf.RoundToInt(gridSpacing * 1000f)}";

    private void EnsureCollider()
    {
        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();
    }

    private int ComputeShapeHash(float gridSpacing)
    {
        EnsureCollider();

        unchecked
        {
            int h = 17;
            h = h * 31 + polygonCollider.pathCount;
            h = h * 31 + Mathf.RoundToInt(gridSpacing * 1000f);

            for (int p = 0; p < polygonCollider.pathCount; p++)
            {
                var pts = polygonCollider.GetPath(p);
                for (int i = 0; i < pts.Length; i++)
                {
                    int x = Mathf.RoundToInt(pts[i].x * 1000f);
                    int y = Mathf.RoundToInt(pts[i].y * 1000f);
                    h = h * 31 + x;
                    h = h * 31 + y;
                }
            }

            var t = transform;
            int px = Mathf.RoundToInt(t.position.x * 1000f);
            int py = Mathf.RoundToInt(t.position.y * 1000f);
            int rz = Mathf.RoundToInt(t.eulerAngles.z * 1000f);
            var s = t.lossyScale;
            int sx = Mathf.RoundToInt(s.x * 1000f);
            int sy = Mathf.RoundToInt(s.y * 1000f);

            h = h * 31 + px; h = h * 31 + py;
            h = h * 31 + rz;
            h = h * 31 + sx; h = h * 31 + sy;

            h = h * 31 + (int)rbInput.constraintType;

            return h;
        }
    }

    private bool TryGetCachedMassProps(float gridSpacing, out RBCalcCache cache)
    {
        EnsureCollider();

        int hash = ComputeShapeHash(gridSpacing);
        lock (_memCacheLock)
        {
            if (_memCache.TryGetValue(hash, out cache))
                return true;
        }

        if (!Application.isEditor)
        {
            string key = PrefKey(hash, gridSpacing);
            if (PlayerPrefs.HasKey(key))
            {
                var s = PlayerPrefs.GetString(key);
                var parts = s.Split('|');
                if (parts.Length == 4 &&
                    float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float cx) &&
                    float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float cy) &&
                    int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int np) &&
                    double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out double sum))
                {
                    cache = new RBCalcCache
                    {
                        shapeHash = hash,
                        gridSpacing = gridSpacing,
                        centroidWorld = new Vector2(cx, cy),
                        numPoints = np,
                        sumR2AroundCentroid = sum
                    };
                    lock (_memCacheLock) _memCache[hash] = cache;
                    return true;
                }
            }
        }

        cache = default;
        return false;
    }

    private void SaveCachedMassProps(in RBCalcCache cache)
    {
        lock (_memCacheLock) _memCache[cache.shapeHash] = cache;

        if (!Application.isEditor)
        {
            string key = PrefKey(cache.shapeHash, cache.gridSpacing);
            string val = string.Concat(
                cache.centroidWorld.x.ToString("R", CultureInfo.InvariantCulture), "|",
                cache.centroidWorld.y.ToString("R", CultureInfo.InvariantCulture), "|",
                cache.numPoints.ToString(CultureInfo.InvariantCulture), "|",
                cache.sumR2AroundCentroid.ToString("R", CultureInfo.InvariantCulture)
            );
            PlayerPrefs.SetString(key, val);
        }
    }

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

        bool userIsModifying = Tools.current == Tool.Move || Tools.current == Tool.Rotate || Tools.current == Tool.Scale;
        if (userIsModifying) return;

        if (polygonCollider == null) polygonCollider = GetComponent<PolygonCollider2D>();

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

        if (lastFramePosition != (Vector2)transform.localPosition)
        {
            lastFramePosition = transform.localPosition;
            framesSinceLastPositionChange = 0;
        }
        else framesSinceLastPositionChange++;

        if (framesSinceLastPositionChange < 20) return;

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
        (bool hasAltCentroid, Vector2 altCentroid) = GetAlternativeCentroid();
        if (hasAltCentroid) return altCentroid;

        if (TryGetCachedMassProps(gridSpacing, out var cache))
        {
            cachedCentroid = cache.centroidWorld;
            ApproximateVolume(cache.numPoints, gridSpacing);
            return cachedCentroid;
        }

        Vector2[] points = GeneratePoints(gridSpacing, Vector2.zero);
        int numPoints = points.Length;
        if (numPoints == 0) return Vector2.zero;

        ApproximateVolume(numPoints, gridSpacing);

        Vector2 centroid = Vector2.zero;
        foreach (Vector2 point in points) centroid += point;
        centroid /= numPoints;

        cachedCentroid = centroid;

        double sumR2 = 0.0;
        object lockObj = new object();
        Parallel.For(0, numPoints,
            () => 0.0,
            (i, _, local) => local + (points[i] - centroid).sqrMagnitude,
            local => { lock (lockObj) sumR2 += local; });

        var newCache = new RBCalcCache
        {
            shapeHash = ComputeShapeHash(gridSpacing),
            gridSpacing = gridSpacing,
            centroidWorld = centroid,
            numPoints = numPoints,
            sumR2AroundCentroid = sumR2
        };
        SaveCachedMassProps(newCache);

        return centroid;
    }

    public (float, float) ComputeInertiaAndBalanceRigidBody(
        ref Vector2[] vectors,
        ref Vector2 rigidBodyPosition,
        Vector2 offset,
        float? gridDensityInput = null
    ) {
        float gridSpacing = gridDensityInput ?? 0.2f;

        bool canUseCache = !rbInput.overrideCentroid && rbInput.constraintType != ConstraintType.LinearMotor;

        if (canUseCache && TryGetCachedMassProps(gridSpacing, out var cache) && cache.numPoints > 0)
        {
            ApproximateVolume(cache.numPoints, gridSpacing);

            cachedCentroid = cache.centroidWorld;
            Vector2 centroid2 = cachedCentroid;

            Vector2 shift = rigidBodyPosition - centroid2;
            for (int i = 0; i < vectors.Length; i++)
                vectors[i] += shift;

            rigidBodyPosition = centroid2;

            float inertia = (rbInput.mass / Mathf.Max(1, cache.numPoints)) * (float)cache.sumR2AroundCentroid;

            float maxRadiusSqr = 0.0f;
            foreach (Vector2 vec in vectors)
            {
                Vector2 validatedVec = vec;
                if (validatedVec.x > 50000) validatedVec.x -= 100000;
                maxRadiusSqr = Mathf.Max(maxRadiusSqr, validatedVec.sqrMagnitude);
            }
            maxRadiusSqr += 1.0f;

            return (inertia, maxRadiusSqr);
        }

        Vector2[] points = GeneratePoints(gridSpacing, offset);
        int numPoints = points.Length;
        if (numPoints == 0) return (0f, 0f);

        ApproximateVolume(numPoints, gridSpacing);

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

        Vector2 shift2 = rigidBodyPosition - centroid;
        for (int i = 0; i < vectors.Length; i++)
            vectors[i] += shift2;

        rigidBodyPosition = centroid;

        // IMPORTANT: copy ref var to local BEFORE lambda
        Vector2 center = rigidBodyPosition;

        double sumR2 = 0.0;
        object lockObj2 = new object();
        Parallel.For(0, numPoints,
            () => 0.0,
            (i, _, local) => local + (points[i] - center).sqrMagnitude,
            local => { lock (lockObj2) sumR2 += local; });

        float pointMass = rbInput.mass / numPoints;
        float inertiaOut = (float)(sumR2 * pointMass);

        float maxRadiusSqrOut = 0.0f;
        foreach (Vector2 vec in vectors)
        {
            Vector2 validatedVec = vec;
            if (validatedVec.x > 50000) validatedVec.x -= 100000;
            maxRadiusSqrOut = Mathf.Max(maxRadiusSqrOut, validatedVec.sqrMagnitude);
        }
        maxRadiusSqrOut += 1.0f;

        if (!hasAltCentroid && rbInput.constraintType != ConstraintType.LinearMotor)
        {
            var newCache = new RBCalcCache
            {
                shapeHash = ComputeShapeHash(gridSpacing),
                gridSpacing = gridSpacing,
                centroidWorld = centroid,
                numPoints = numPoints,
                sumR2AroundCentroid = sumR2
            };
            SaveCachedMassProps(newCache);
        }

        return (inertiaOut, maxRadiusSqrOut);
    }

    private void ApproximateVolume(int numPoints, float gridSpacing)
    {
        if (!Application.isPlaying) gridSpacing *= 2;

        if (PM.Instance.main == null) PM.Instance.main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();

        float approxVolume = numPoints * Func.Sqr(gridSpacing * PM.Instance.main.SimUnitToMetersFactor) * PM.Instance.main.ZDepthMeters * 1000;
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