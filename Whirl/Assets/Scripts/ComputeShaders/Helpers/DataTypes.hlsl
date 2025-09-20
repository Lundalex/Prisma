// --- Particle Simulation structs ---

struct Mat
{
    // Color (RGB) rect in the atlas
    int2  colTexLoc;
    int2  colTexDims;

    // Existing UV transform/tile flags
    float2 sampleOffset;
    float  colTexUpScaleFactor;

    // Existing material tints/params
    float3 baseCol;
    float  opacity;
    float3 sampleColMul;
    float3 edgeCol;

    // Lighting
    float edgeRoundingMult;
};
struct PType 
{
    // Inter-Particle Springs
    int fluidSpringGroup;
    float springPlasticity;
    float springStiffness;
    float springTolDeformation;

    // Thermals
    float thermalConductivity;
    float specificHeatCapacity;
    float freezeThreshold;
    float vaporizeThreshold;

    // Inter-particle Forces
    float pressure;
    float nearPressure;
    float viscosity;
    float gravity;

    // Particle Properties
    float mass;
    float targetDensity;
    float damping;
    float passiveDamping;

    // Material
    int matIndex;

    // Simulation Engine
    float influenceRadius;
};
struct PData
{
    float2 predPos;
    float2 pos;
    float2 vel;
    float2 lastVel;
    float density;
    float nearDensity;
    int lastChunkKey_PType_POrder; // composed 3 int structure
    float temperature; // kelvin
    float temperatureExchangeBuffer;
    float recordedPressure;
    // POrder; // POrder is dynamic, 
    // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
    // PType; // 0 <= PType <= PTypesNum
};
struct Spring 
{
    int pLinkedA;
    int pLinkedB;
    float restLength;
};
struct RecordedFluidData
{
    int totTemp_Int;
    int totThermalEnergy_Int;
    int totPressure_Int;
    int2 totRigidBodyForces_Int2;
    int2 totVel_Int2;
    int totVelAbs_Int;
    int totMass_Int;

    int numContributions;
};

// --- Rigid Body structs ---

struct RigidBody 
{
    float2 pos;
    int2 vel_AsInt2;
    float2 nextPos;
    float2 nextVel;
    int rotVel_AsInt; // (radians / second)
    float totRot;
    float mass; // 0 -> Stationary
    float inertia;
    float gravity;
    float rbElasticity;
    float fluidElasticity;
    float friction;
    float passiveDamping;
    float maxRadiusSqr;
    int startIndex;
    int endIndex;

    // Inter-RB spring constraint
    int linkedRBIndex; // -1 -> No link
    float springRestLength;
    float springStiffness; // 0 -> Fully rigid constraint
    float damping;
    float2 localLinkPosThisRB;
    float2 localLinkPosOtherRB;

    // Linear motor (link Type)
    float lerpSpeed;
    float lerpTimeOffset;

    // Heating
    float heatingStrength;

    // Recorded spring force
    float2 recordedSpringForce;
    float2 recordedFrictionForce;

    // Display
    int renderPriority;
    int matIndex;
    int springMatIndex;

    // State flags
    int stateFlags;
};

struct RBVector 
{
    float2 pos;
    int parentIndex;
};

struct SensorArea
{
    float2 min;
    float2 max;
    float patternMod;
    float4 lineColor;
    float4 colorTint;
};

struct RBAdjustment
{
    int2 deltaPos_Int2;
    int2 deltaVel_Int2;
    int deltaRotVel_Int;

    int2 recordedSpringForce_Int2;
    int2 recordedFrictionForce_Int2;
};

struct RBHitInfo
{
    float dst;
    float2 hitPoint;
    float2 pointPos;
    float2 lineVec;
};

struct ImpulseData
{
    float2 centerImpulse;
    float rotImpulse;
    int rbIndex;
};

Spring InitSpring(int pLinkedA, int pLinkedB, float restLength)
{
    Spring spring;
    spring.pLinkedA = pLinkedA;
    spring.pLinkedB = pLinkedB;
    spring.restLength = restLength;

    return spring;
}

RecordedFluidData InitRecordedFluidData()
{
    RecordedFluidData recordedFluidData;
    recordedFluidData.totTemp_Int = 0;
    recordedFluidData.totThermalEnergy_Int = 0;
    recordedFluidData.totPressure_Int = 0;
    recordedFluidData.totRigidBodyForces_Int2 = 0;
    recordedFluidData.totVel_Int2 = 0;
    recordedFluidData.totVelAbs_Int = 0;
    recordedFluidData.totMass_Int = 0;
    recordedFluidData.numContributions = 0;

    return recordedFluidData;
}

RBAdjustment InitRBAdjustment()
{
    RBAdjustment rbAdjustment;
    rbAdjustment.deltaPos_Int2 = 0;
    rbAdjustment.deltaVel_Int2 = 0;
    rbAdjustment.deltaRotVel_Int = 0;
    rbAdjustment.recordedSpringForce_Int2 = 0;
    rbAdjustment.recordedFrictionForce_Int2 = 0;

    return rbAdjustment;
}

RBHitInfo InitRBHitInfo()
{
    RBHitInfo rbHitInfo;
    rbHitInfo.dst = 1.#INF;
    rbHitInfo.hitPoint = 1.#INF;
    rbHitInfo.pointPos = 1.#INF;
    rbHitInfo.lineVec = 0;

    return rbHitInfo;
}

RBHitInfo InitRBHitInfo(float dst, float2 hitPoint, float2 pointPos, float2 lineVec)
{
    RBHitInfo rbHitInfo;
    rbHitInfo.dst = dst;
    rbHitInfo.hitPoint = hitPoint;
    rbHitInfo.pointPos = pointPos;
    rbHitInfo.lineVec = lineVec;

    return rbHitInfo;
}

ImpulseData InitImpulseData(float3 combinedImpulse, int rbIndex)
{
    ImpulseData impulseData;
    impulseData.centerImpulse = combinedImpulse.xy;
    impulseData.rotImpulse = combinedImpulse.z;
    impulseData.rbIndex = rbIndex;

    return impulseData;
}