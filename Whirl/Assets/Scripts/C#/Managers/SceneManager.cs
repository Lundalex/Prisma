using System;
using System.Collections.Generic;
using System.Linq;
using Resources2;
using Unity.Mathematics;
using UnityEngine;

public class SceneManager : MonoBehaviour
{
    public int MaxAtlasDims;

    private Vector2 sceneMin;
    private Vector2 sceneMax;
    private bool referencesHaveBeenSet = false;

    private Transform sensorUIContainer;
    private Transform sensorOutlineContainer;
    private Main main;
    private ArrowManager arrowManager;
    private SensorManager sensorManager;

    private struct CachedAtlas
    {
        public Texture2D atlas;
        public Dictionary<int, Rect> rectByTexId;
        public List<Texture2D> sources;
        public int maxDims;
    }

    private static CachedAtlas _cachedAtlas;

    private void SetReferences()
    {
        sensorUIContainer = GameObject.FindGameObjectWithTag("SensorUIContainer").GetComponent<Transform>();
        sensorOutlineContainer = GameObject.FindGameObjectWithTag("SensorOutlineContainer").GetComponent<Transform>();
        main = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Main>();
        arrowManager = GameObject.FindGameObjectWithTag("ArrowManager").GetComponent<ArrowManager>();
        sensorManager = GameObject.FindGameObjectWithTag("SensorManager").GetComponent<SensorManager>();

        referencesHaveBeenSet = true;
    }

    public void DestroyRuntimeSensorObjects()
    {
        if (!referencesHaveBeenSet) SetReferences();

        // Wipe UI widgets
        if (sensorUIContainer != null)
        {
            for (int i = sensorUIContainer.childCount - 1; i >= 0; i--)
                Destroy(sensorUIContainer.GetChild(i).gameObject);
        }

        if (sensorOutlineContainer != null)
        {
            for (int i = sensorOutlineContainer.childCount - 1; i >= 0; i--)
                Destroy(sensorOutlineContainer.GetChild(i).gameObject);
        }
    }

    public int2 GetBounds(int maxInfluenceRadius)
    {
        int2 bounds = new(Mathf.CeilToInt(transform.localScale.x), Mathf.CeilToInt(transform.localScale.y));
        int2 boundsMod = bounds % maxInfluenceRadius;
        if (boundsMod.x != 0) bounds.x += maxInfluenceRadius - boundsMod.x;
        if (boundsMod.y != 0) bounds.y += maxInfluenceRadius - boundsMod.y;
        return bounds;
    }

    // NEW: single call to provide world-space bounds without per-point recomputation
    public void GetSceneBounds(out Vector2 min, out Vector2 max)
    {
        if (!referencesHaveBeenSet) SetReferences();

        sceneMin.x = transform.position.x - transform.localScale.x * 0.5f + main.FluidPadding;
        sceneMin.y = transform.position.y - transform.localScale.y * 0.5f + main.FluidPadding;
        sceneMax.x = transform.position.x + transform.localScale.x * 0.5f - main.FluidPadding;
        sceneMax.y = transform.position.y + main.FluidPadding + transform.localScale.y * 0.5f - main.FluidPadding;

        min = sceneMin;
        max = sceneMax;
    }

    public bool IsPointInsideBounds(Vector2 point)
    {
        if (!referencesHaveBeenSet) SetReferences();

        sceneMin.x = transform.position.x - transform.localScale.x * 0.5f + main.FluidPadding;
        sceneMin.y = transform.position.y - transform.localScale.y * 0.5f + main.FluidPadding;
        sceneMax.x = transform.position.x + transform.localScale.x * 0.5f - main.FluidPadding;
        sceneMax.y = transform.position.y + main.FluidPadding + transform.localScale.y * 0.5f - main.FluidPadding;

        bool isInsideBounds = point.x > sceneMin.x
                              && point.y > sceneMin.y
                              && point.x < sceneMax.x
                              && point.y < sceneMax.y;

        return isInsideBounds;
    }

    public bool IsSpaceEmpty(Vector2 point, SceneFluid thisFluid, SceneRigidBody[] allRigidBodies, SceneFluid[] allFluids)
    {
        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            ColliderType colliderType = rigidBody.rbInput.colliderType;
            bool isFluidCollider = colliderType == ColliderType.Fluid || colliderType == ColliderType.All;
            if (rigidBody.IsPointInsidePolygon(point) && isFluidCollider) return false;
        }

        SceneFluid[] sortedFluids = allFluids
            .OrderBy(fluid => fluid.transform.GetSiblingIndex())
            .ToArray();

        int thisFluidIndex = Array.IndexOf(sortedFluids, thisFluid);
        for (int i = 0; i < thisFluidIndex; i++)
        {
            if (sortedFluids[i].IsPointInsidePolygon(point)) return false;
        }

        return true;
    }

    public (Texture2D, Mat[]) ConstructTextureAtlas(CustomMat[] materials)
    {
        var requestedUnique = new List<Texture2D>();
        var seenRequested = new HashSet<int>();
        int[] matTexIds = new int[materials.Length];

        for (int i = 0; i < materials.Length; i++)
        {
            Texture2D colTex = GetColTexture(materials[i]);
            if (colTex != null)
            {
                int id = colTex.GetInstanceID();
                matTexIds[i] = id;
                if (seenRequested.Add(id))
                    requestedUnique.Add(colTex);

                if (!colTex.isReadable)
                    Debug.LogWarning("Texture " + colTex.name + " is not readable. Enable Read/Write for atlas packing.");
            }
            else
            {
                matTexIds[i] = 0;
            }
        }

        bool hasCached = _cachedAtlas.atlas != null;
        bool dimsChanged = hasCached && _cachedAtlas.maxDims != MaxAtlasDims;

        if (hasCached && !dimsChanged)
        {
            var rects = _cachedAtlas.rectByTexId;
            bool allPresent = true;
            for (int i = 0; i < requestedUnique.Count; i++)
            {
                if (!rects.ContainsKey(requestedUnique[i].GetInstanceID()))
                {
                    allPresent = false;
                    break;
                }
            }

            if (allPresent)
            {
                var mats = BuildMats(materials, matTexIds, _cachedAtlas.atlas, _cachedAtlas.rectByTexId);
                StringUtils.LogIfInEditor($"[Atlas] Reused cached atlas ({_cachedAtlas.atlas.width}x{_cachedAtlas.atlas.height}) with {_cachedAtlas.rectByTexId.Count} textures");
                return (_cachedAtlas.atlas, mats);
            }
        }

        var union = new List<Texture2D>();
        var seenUnion = new HashSet<int>();

        if (hasCached)
        {
            foreach (var t in _cachedAtlas.sources)
            {
                if (t == null) continue;
                if (seenUnion.Add(t.GetInstanceID()))
                    union.Add(t);
            }
        }

        foreach (var t in requestedUnique)
        {
            if (t == null) continue;
            if (seenUnion.Add(t.GetInstanceID()))
                union.Add(t);
        }

        var (newAtlas, rectByTexId) = PackUnionIntoNewAtlas(union, MaxAtlasDims);

        if (hasCached && _cachedAtlas.atlas != null && _cachedAtlas.atlas != newAtlas)
        {
            UnityEngine.Object.Destroy(_cachedAtlas.atlas);
        }

        _cachedAtlas = new CachedAtlas
        {
            atlas = newAtlas,
            rectByTexId = rectByTexId,
            sources = union,
            maxDims = MaxAtlasDims
        };

        var newMats = BuildMats(materials, matTexIds, newAtlas, rectByTexId);
        return (newAtlas, newMats);
    }

    private static (Texture2D atlas, Dictionary<int, Rect> rects) PackUnionIntoNewAtlas(List<Texture2D> textures, int maxDims)
    {
        textures.Sort((a, b) => a.GetInstanceID().CompareTo(b.GetInstanceID()));

        Texture2D atlas = new Texture2D(maxDims, maxDims, TextureFormat.RGBAHalf, false);
        Rect[] rectsArr = textures.Count > 0
            ? atlas.PackTextures(textures.ToArray(), 1, maxDims)
            : Array.Empty<Rect>();

        atlas.Apply(false, true);

        var dict = new Dictionary<int, Rect>(textures.Count);
        for (int i = 0; i < textures.Count; i++)
            dict[textures[i].GetInstanceID()] = rectsArr[i];

        float sizeMB = (atlas.width * atlas.height * 8f) / (1024f * 1024f);
        StringUtils.LogIfInEditor($"[Atlas] New atlas Packed ({atlas.width}x{atlas.height}) with {textures.Count} textures, ~{sizeMB:0.00} MB");
        return (atlas, dict);
    }

    private Mat[] BuildMats(CustomMat[] materials, int[] matTexIds, Texture2D atlas, Dictionary<int, Rect> rectByTexId)
    {
        int2 GetTexLoc(Rect rect)  => new((int)(rect.x * atlas.width), (int)(rect.y * atlas.height));
        int2 GetTexDims(Rect rect) => new((int)(rect.width * atlas.width), (int)(rect.height * atlas.height));

        Mat[] renderMats = new Mat[materials.Length];
        for (int i = 0; i < materials.Length; i++)
        {
            CustomMat bm = materials[i];
            int texId = matTexIds[i];

            Rect r = new();
            bool hasCol = texId != 0 && rectByTexId.TryGetValue(texId, out r);
            int2 colLoc  = hasCol ? GetTexLoc(r)  : new int2(-1, -1);
            int2 colDims = hasCol ? GetTexDims(r) : new int2(-1, -1);

            renderMats[i] = InitMat(
                bm,
                bm != null ? bm.BaseColor : new float3(0,0,0),
                colLoc, colDims,
                bm != null ? bm.sampleOffset : new float2(0,0)
            );
        }

        return renderMats;
    }

    private static Texture2D GetColTexture(CustomMat bm)
    {
        if (bm == null) return null;

        if (bm is SimpleMat mi && mi.colTexture != null)
            return mi.colTexture;

        if (bm is RenderMat rm)
        {
            if (rm.bakedTexture != null)
                return rm.bakedTexture;

            Debug.LogWarning($"RenderMat '{rm.name}' has no bakedTexture; skipping in atlas.");
            return null;
        }

        return null;
    }

    private Mat InitMat(CustomMat customMat,
                        float3 baseCol,
                        int2 colTexLoc, int2 colTexDims,
                        float2 sampleOffset)
    {
        float upScale = (customMat != null) ? customMat.colorTextureUpScaleFactor : 1.0f;
        bool disableMirror = (customMat != null) && customMat.disableMirrorRepeat;

        return new Mat
        {
            colTexLoc = colTexLoc,
            colTexDims = colTexDims,

            sampleOffset = sampleOffset,
            colTexUpScaleFactor = disableMirror ? -upScale : upScale,

            baseCol = baseCol,
            opacity = Mathf.Clamp(customMat != null ? customMat.opacity : 1.0f, 0.0f, 1.0f),
            sampleColMul = customMat != null ? customMat.SampleColor : new float3(1, 1, 1),
            edgeCol = (customMat != null && customMat.transparentEdges) ? new float3(-1, -1, -1)
                                                                        : (customMat != null ? customMat.edgeColor : new float3(0, 0, 0)),

            edgeRoundingMult = customMat != null ? customMat.edgeRoundingMultiplier : 1.0f,
            metallicity = customMat.metallicity
        };
    }

    public PData[] GenerateParticles(int maxParticlesNum, float gridSpacing = 0)
    {
        if (maxParticlesNum == 0) return new PData[0];

        SceneFluid[] allFluids = GetAllSceneFluids();
        Vector2 offset = GetBoundsOffset();

        List<PData> allPDatas = new();
        foreach (SceneFluid fluid in allFluids)
        {
            PData[] pDatas = fluid.GenerateParticles(offset, gridSpacing);
            foreach (var pData in pDatas)
            {
                allPDatas.Add(pData);
                if (--maxParticlesNum <= 0) return allPDatas.ToArray();
            }
        }

        return allPDatas.ToArray();
    }

    public (RBData[], RBVector[], SensorArea[]) CreateRigidBodies(float? rbCalcGridSpacingInput = null)
    {
        float rbCalcGridSpacing = rbCalcGridSpacingInput ?? 1.0f;
        if (!referencesHaveBeenSet) SetReferences();

        SceneRigidBody[] allRigidBodies = GetAllSceneRigidBodies();

        foreach (SceneRigidBody rigidBody in allRigidBodies)
            rigidBody.ComputeCentroid(rbCalcGridSpacing);

        foreach (SceneRigidBody rigidBody in allRigidBodies)
        {
            if (rigidBody.polygonCollider == null)
                rigidBody.polygonCollider = rigidBody.GetComponent<PolygonCollider2D>();

            int pathCount = rigidBody.polygonCollider.pathCount;
            for (int p = 0; p < pathCount; p++)
            {
                Vector2[] pathPoints = rigidBody.polygonCollider.GetPath(p);
                pathPoints = ArrayUtils.RemoveAdjacentDuplicates(pathPoints);
                rigidBody.polygonCollider.SetPath(p, pathPoints);
            }
        }

        Vector2 boundsOffset = GetBoundsOffset();

        List<RBData> allRBData = new();
        List<RBVector> allRBVectors = new();
        List<SensorBase> sensors = new();

        for (int i = 0; i < allRigidBodies.Length; i++)
        {
            SceneRigidBody rigidBody = allRigidBodies[i];
            if (!rigidBody.rbInput.includeInSimulation) continue;

            Transform transform = rigidBody.transform;
            Vector2 parentOffset = transform.position - transform.localPosition;

            Vector2[] vectors = GetTransformedMultiPathPoints(rigidBody, boundsOffset, out Vector2 transformedRBPos);
            if (rigidBody.addInBetweenPoints)
                AddInBetweenPoints(ref vectors, rigidBody.doRecursiveSubdivisison, rigidBody.minDstForSubDivision);

            (float inertia, float maxRadiusSqr) = rigidBody.ComputeInertiaAndBalanceRigidBody(
                ref vectors, ref transformedRBPos, boundsOffset, rbCalcGridSpacing
            );

            RBInput rbInput = rigidBody.rbInput;
            int springLinkedRBIndex = rbInput.linkedRigidBody == null
                ? -1
                : Array.IndexOf(allRigidBodies, rbInput.linkedRigidBody);

            if (rbInput.constraintType == ConstraintType.Spring && springLinkedRBIndex == -1)
            {
                Debug.LogError("Linked rigid body not set. SceneRigidBody: " + rigidBody.name);
            }
            else if (i == springLinkedRBIndex)
            {
                Debug.LogWarning("Spring to self. Removed.");
                rbInput.constraintType = ConstraintType.None;
            }

            float springRestLength = rbInput.springRestLength;
            if (rbInput.linkedRigidBody != null && rbInput.autoSpringRestLength)
            {
                float distance = Vector2.Distance(
                    rigidBody.cachedCentroid + rbInput.localLinkPosThisRB,
                    rbInput.linkedRigidBody.cachedCentroid + rbInput.localLinkPosOtherRB
                );
                springRestLength = distance;
            }

            int startIndex = allRBVectors.Count;
            foreach (Vector2 v in vectors)
                allRBVectors.Add(new RBVector(v, i));
            int endIndex = allRBVectors.Count - 1;

            int matIndex = -1;
            int springMatIndex = -1;
            if (main != null && main.MatIndexMap != null)
            {
                if (rigidBody.material != null && main.MatIndexMap.TryGetValue(rigidBody.material, out int _m))
                    matIndex = _m;

                if (!rbInput.disableSpringRender &&
                    rigidBody.springMaterial != null &&
                    main.MatIndexMap.TryGetValue(rigidBody.springMaterial, out int _sm))
                    springMatIndex = _sm;
            }

            allRBData.Add(InitRBData(
                rbInput,
                inertia,
                maxRadiusSqr,
                springLinkedRBIndex,
                springRestLength,
                startIndex,
                endIndex,
                transformedRBPos,
                parentOffset,
                matIndex,
                springMatIndex
            ));

            foreach (SensorBase sensor in rigidBody.linkedSensors)
            {
                if (sensor == null) continue;
                if (!sensor.isActiveAndEnabled) continue;

                if (sensors.Contains(sensor))
                {
                    Debug.LogWarning("Duplicate sensor " + sensor.name);
                }
                else
                {
                    if (sensor is RigidBodySensor rigidBodySensor)
                    {
                        rigidBodySensor.linkedRBIndex = i;
                        sensors.Add(sensor);

                        rigidBodySensor.SetReferences(sensorUIContainer, sensorOutlineContainer, main, sensorManager);
                        rigidBodySensor.Initialize(transformedRBPos);
                    }
                    else if (sensor is RigidBodyArrow rigidBodyArrow)
                    {
                        rigidBodyArrow.linkedRBIndex = i;
                        sensors.Add(sensor);

                        rigidBodyArrow.SetReferences(arrowManager, main, sensorManager);
                        rigidBodyArrow.Initialize();
                    }
                }
            }
        }

        List<SensorArea> sensorAreas = new();

        GameObject[] fluidSensorObjects = GameObject.FindGameObjectsWithTag("FluidSensor");
        FluidSensor[] fluidSensors = Array.ConvertAll(fluidSensorObjects, obj => obj.GetComponent<FluidSensor>());
        foreach (FluidSensor fluidSensor in fluidSensors)
        {
            if (fluidSensor == null) continue;
            sensors.Add(fluidSensor);

            fluidSensor.SetReferences(sensorUIContainer, sensorOutlineContainer, main, sensorManager);
            fluidSensor.Initialize(Vector2.zero);

            sensorAreas.Add(fluidSensor.GetSensorAreaData());
        }

        GameObject[] fluidArrowFieldObjects = GameObject.FindGameObjectsWithTag("FluidArrowField");
        FluidArrowField[] fluidArrowFields = Array.ConvertAll(fluidArrowFieldObjects, obj => obj.GetComponent<FluidArrowField>());
        foreach (FluidArrowField fluidArrowField in fluidArrowFields)
        {
            if (fluidArrowField == null) continue;
            sensors.Add(fluidArrowField);

            fluidArrowField.SetReferences(arrowManager, main, sensorManager);
            fluidArrowField.Initialize();

            if (fluidArrowField.doRenderMeasurementZone) sensorAreas.Add(fluidArrowField.GetSensorAreaData());
        }

        sensorManager.sensors = sensors;

        return (allRBData.ToArray(), allRBVectors.ToArray(), sensorAreas.ToArray());
    }

    private Vector2[] GetTransformedMultiPathPoints(SceneRigidBody rigidBody, Vector2 offset, out Vector2 transformedRBPos)
    {
        List<Vector2> combined = new();
        PolygonCollider2D poly = rigidBody.GetComponent<PolygonCollider2D>();

        for (int p = 0; p < poly.pathCount; p++)
        {
            Vector2[] pathPoints = poly.GetPath(p);

            for (int i = 0; i < pathPoints.Length; i++)
            {
                Vector2 worldPt = rigidBody.transform.TransformPoint(pathPoints[i]);
                if (p > 0 && i == 0) worldPt.x += Main.PathFlagOffset;
                combined.Add(worldPt);
            }
        }

        transformedRBPos = (Vector2)rigidBody.transform.position + offset;
        for (int i = 0; i < combined.Count; i++)
            combined[i] = combined[i] + offset - transformedRBPos;

        return combined.ToArray();
    }

    public static void AddInBetweenPoints(ref Vector2[] vectors, bool doRecursiveSubdivisison, float minDst)
    {
        if (doRecursiveSubdivisison)
        {
            minDst = Mathf.Max(minDst, 0.5f);
            AddInBetweenPointsRecursively(ref vectors, minDst);
        }
        else
        {
            int numVectors = vectors.Length;
            List<Vector2> newVectors = new();

            int pathStartIndex = 0;
            Vector2 firstPathVec = vectors[0];
            Vector2 lastVec = firstPathVec;
            newVectors.Add(lastVec);

            for (int i = 1; i <= numVectors; i++)
            {
                bool endOfArray = i == numVectors;
                int vecIndex = endOfArray ? pathStartIndex : i;
                Vector2 nextVec = vectors[vecIndex];

                Vector2 inBetween;
                bool newPathFlag = nextVec.x > Main.PathFlagThreshold;
                if (newPathFlag)
                {
                    nextVec.x -= Main.PathFlagOffset;
                    float randOffset = UnityEngine.Random.Range(-0.05f, 0.05f);
                    inBetween = (lastVec * (1 + randOffset) + firstPathVec * (1 - randOffset)) / 2.0f;
                    firstPathVec = nextVec;
                    lastVec = nextVec;
                    pathStartIndex = vecIndex;
                    nextVec.x += Main.PathFlagOffset;
                }
                else
                {
                    inBetween = (lastVec + nextVec) / 2.0f;
                    lastVec = nextVec;
                }

                newVectors.Add(inBetween);
                if (!endOfArray) newVectors.Add(nextVec);
            }

            vectors = newVectors.ToArray();
        }
    }

    private static void AddInBetweenPointsRecursively(ref Vector2[] vectors, float minDst)
    {
        bool needsSubdivision = false;
        List<Vector2> newVectors = new();

        int count = vectors.Length;
        for (int i = 0; i < count; i++)
        {
            Vector2 current = vectors[i];
            Vector2 next = vectors[(i + 1) % count];

            newVectors.Add(current);

            bool currentIsMarker = current.x > Main.PathFlagThreshold;
            bool nextIsMarker = next.x > Main.PathFlagThreshold;

            if (!currentIsMarker && !nextIsMarker)
            {
                float distance = Vector2.Distance(current, next);
                if (distance > minDst)
                {
                    Vector2 inBetween = (current + next) / 2f;
                    newVectors.Add(inBetween);
                    needsSubdivision = true;
                }
            }
        }

        vectors = newVectors.ToArray();

        if (needsSubdivision)
            AddInBetweenPointsRecursively(ref vectors, minDst);
    }

    private Vector2 GetBoundsOffset()
    {
        return new Vector2(
            transform.localScale.x * 0.5f - transform.position.x,
            transform.localScale.y * 0.5f - transform.position.y
        );
    }

    public static SceneRigidBody[] GetAllSceneRigidBodies()
    {
        List<GameObject> rigidBodyObjects = GameObject.FindGameObjectsWithTag("RigidBody").ToList();
        List<SceneRigidBody> validRigidBodies = new();
        foreach (GameObject rigidBodyObject in rigidBodyObjects)
        {
            SceneRigidBody rb = rigidBodyObject.GetComponent<SceneRigidBody>();
            if (rb.rbInput.includeInSimulation) validRigidBodies.Add(rb);
        }
        return validRigidBodies.ToArray();
    }

    public static SceneFluid[] GetAllSceneFluids()
    {
        GameObject[] fluidObjects = GameObject.FindGameObjectsWithTag("Fluid");
        SceneFluid[] allFluids = new SceneFluid[fluidObjects.Length];
        for (int i = 0; i < fluidObjects.Length; i++)
            allFluids[i] = fluidObjects[i].GetComponent<SceneFluid>();
        return allFluids;
    }

    private RBData InitRBData(RBInput rbInput,
                              float inertia,
                              float maxRadiusSqr,
                              int linkedRBIndex,
                              float springRestLength,
                              int startIndex,
                              int endIndex,
                              Vector2 pos,
                              Vector2 parentOffset,
                              int matIndex,
                              int springMatIndex)
    {
        bool canMove = rbInput.canMove && rbInput.constraintType != ConstraintType.LinearMotor;
        bool isRBCollider = rbInput.colliderType == ColliderType.RigidBody || rbInput.colliderType == ColliderType.All;
        bool isFluidCollider = rbInput.colliderType == ColliderType.Fluid || rbInput.colliderType == ColliderType.All;
        bool isLinearMotor = rbInput.constraintType == ConstraintType.LinearMotor;
        bool isRigidConstraint = rbInput.constraintType == ConstraintType.Rigid;
        bool isSpringConstraint = rbInput.constraintType == ConstraintType.Spring;

        int stateFlags = 0;
        Func.SetBit(ref stateFlags, 0, false);
        Func.SetBit(ref stateFlags, 1, rbInput.disallowBorderCollisions);

        return new RBData
        {
            pos = pos,
            vel_AsInt2 = rbInput.canMove ? Func.Float2AsInt2(rbInput.velocity, main.FloatIntPrecisionRB) : 0,
            nextPos = 0,
            nextVel = 0,
            rotVel_AsInt = rbInput.canRotate
                ? Func.FloatAsInt(rbInput.angularVelocity, 500000.0f)
                : 0,
            totRot = 0,
            mass = canMove
                ? rbInput.mass
                : (isLinearMotor
                    ? (rbInput.doRoundTrip ? -2 : -1)
                    : 0),
            inertia = rbInput.canRotate ? inertia : 0,
            gravity = rbInput.gravity,

            rbElasticity = isRBCollider ? Mathf.Max(rbInput.rbElasticity, 0.05f) : -1,
            fluidElasticity = isFluidCollider ? Mathf.Max(rbInput.fluidElasticity, 0.05f) : -1,
            friction = rbInput.friction,
            passiveDamping = rbInput.passiveDamping,

            maxRadiusSqr = rbInput.isInteractable ? maxRadiusSqr : -maxRadiusSqr,

            startIndex = startIndex,
            endIndex = endIndex,

            linkedRBIndex = (isSpringConstraint || isRigidConstraint) ? linkedRBIndex : -1,
            springRestLength = isRigidConstraint ? 0 : springRestLength,
            springStiffness = isRigidConstraint ? 0 : rbInput.springStiffness,
            damping = isRigidConstraint ? 0 : rbInput.damping,

            localLinkPosThisRB = isLinearMotor
                ? rbInput.startPos + parentOffset
                : rbInput.localLinkPosThisRB,
            localLinkPosOtherRB = isLinearMotor
                ? rbInput.endPos + parentOffset
                : rbInput.localLinkPosOtherRB,

            lerpSpeed = isLinearMotor ? rbInput.lerpSpeed : 0,
            lerpTimeOffset = rbInput.lerpTimeOffset,

            heatingStrength = rbInput.heatingStrength,
            recordedSpringForce = 0,
            recordedFrictionForce = 0,

            renderPriority = rbInput.disableRender ? -1 : rbInput.renderPriority,

            matIndex = matIndex,
            springMatIndex = rbInput.disableSpringRender ? -1 : springMatIndex,

            stateFlags = stateFlags
        };
    }
}