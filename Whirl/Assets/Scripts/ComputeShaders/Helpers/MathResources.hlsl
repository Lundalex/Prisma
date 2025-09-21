static const float PI = 3.14159;
static const float EPSILON = 0.00000001;
static const float LARGE_FLOAT = 1000000000.0;
static const float SmoothViscosityLaplacianFactor = 45 / PI;
static const float SQRT2    = 1.41421356;
static const float EPS_DIR2 = 1e-12;

#include "RandGen.hlsl"
#include "VariableStorage.hlsl"

// -- Optimised SPH kernel functions --

// InteractionInfluence_optimised() is equivelant to InteractionInfluence()
// Using a fast sqrt() had worse performance results than the regular sqrt()
float InteractionInfluence_optimised(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
		result = sqrt(radius - dst);
	}
	return result;
}

float SmoothLiquid_optimised(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
        float dstR_1 = 1 - dstR;
		result = dstR_1 * dstR_1;
	}
	return result;
}

float SmoothLiquidNear_optimised(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
        float dstR_1 = 1 - dstR;
		result = dstR_1 * dstR_1 * dstR_1;
	}
	return result;
}

float SmoothLiquidDer_optimised(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
		result = -2 * (1 - dstR) / radius;
	}
	return result;
}

float SmoothLiquidNearDer_optimised(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
        float dstR_1 = 1 - dstR;
		result = -3 * dstR_1 * dstR_1 / radius;
	}
	return result;
}

float SmoothViscosityLaplacian_optimised(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
		result = SmoothViscosityLaplacianFactor * (radius - dst) / pow(radius, 6);
	}
	return result;
}


// -- Non-optimised SPH kernel functions --

float InteractionInfluence(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
		result = sqrt(radius - dst);
	}
	return result;
}

float SmoothLiquid(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
		result = (1 - dstR)*(1 - dstR);
	}
	return result;
}

float SmoothLiquidDer(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
		result = -2 * (1 - dstR) / radius;
	}
	return result;
}

float SmoothLiquidNear(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
		result = (1 - dstR)*(1 - dstR)*(1 - dstR);
	}
	return result;
}

float SmoothLiquidNearDer(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
        float dstR = dst / radius;
		result = -3 * (1 - dstR)*(1 - dstR) / radius;
	}
	return result;
}

float SmoothViscosityLaplacian(float dst, float radius)
{
	float result = 0;
	if (dst < radius)
	{
		result = 45 / (PI * pow(radius, 6)) * (radius - dst);
	}
	return result;
}

// -- General math functions --


// ΔQ_ij = Δt * avg_k * diff_T * W_ij / dst_ij 
// where:
// avg_k: average thermal conductivity
// W: contact area between the liquids (W(absDst))
// diff_T: difference in temperature
// dst: absDst
// Δt: DeltaTime
float LiquidTemperatureExchangeModel(float avg_k, float diff_T, float W, float dst, float deltaTime)
{
    return deltaTime * avg_k * diff_T * W / dst;
}

float LiquidSpringForceModel(float stiffness, float restLen, float maxLen, float curLen)
{
    return stiffness * (restLen - curLen); // * (1 - curLen/maxLen)?
}

float LiquidSpringPlasticityModel(float plasticity, int sgnDiffMng, float absDiffMng, float tolDeformation, float DeltaTime)
{
    return plasticity * sgnDiffMng * max(0, absDiffMng - tolDeformation) * DeltaTime;
}

float RBPStickynessModel(float stickyness, float dst, float maxDst)
{
	return stickyness * dst * (1 - dst/maxDst);
}

float dot2(float2 dst)
{
	return dot(dst, dst);
}

float sqr(float a)
{
	return a * a;
}

float avg(float a, float b)
{
    return (a + b) * 0.5;
}

float weightedAvg(float valueA, float weightA, float valueB, float weightB)
{
    float denom = weightA + weightB;
    if (denom == 0) return 0.01;
    return (valueA * weightA + valueB * weightB) / denom;
}

float copysign(float mag, float sgn)
{
    return (sgn >= 0.0) ? abs(mag) : -abs(mag);
}

float lerp1D(float posA, float posB, float valA, float valB, float targetVal)
{
    float t = float(targetVal - valA) / float(valB - valA);
    return posA + t * (posB - posA);
}

float cross2D(float2 VectorA, float2 VectorB)
{
    return VectorA.x * VectorB.y - VectorA.y * VectorB.x;
}

float2 rotate2D(float2 vec, float radians)
{
    // Rotation matrix
    float2x2 rotMatrix = float2x2(
        cos(radians), -sin(radians),
        sin(radians), cos(radians)
    );

    // Rotate the vector
    return mul(rotMatrix, vec);
}

// ccw = CounterClockWise
bool ccw(float2 A, float2 B, float2 C)
{
    return (C.y - A.y) * (B.x - A.x) > (B.y - A.y) * (C.x - A.x);
}

// Returns true if the line AB crosses the line CD
bool CheckLinesIntersect(float2 A, float2 B, float2 C, float2 D)
{
    return ccw(A, C, D) != ccw(B, C, D) && ccw(A, B, C) != ccw(A, B, D);
}

bool IsPointToTheLeftOfLine(float2 P, float2 A, float2 B)
{
    // Check if point P is to the left of the line
    bool isToLeft = ((A.y > P.y) != (B.y > P.y)) &&
                    (P.x < (B.x - A.x) * (P.y - A.y) / (B.y - A.y + EPSILON) + A.x);

    return isToLeft;
}

float2 LineIntersectionPoint(float2 r0, float2 r1, float2 a, float2 b)
{
    float2 s1 = r1 - r0;
    float2 s2 = b - a;

    float denom = (-s2.x * s1.y + s1.x * s2.y);

    // Check if lines are parallel or coincident
    if (abs(denom) < EPSILON)
    {
        // Lines are parallel or coincident, no intersection
        return float2(1.#INF, 1.#INF);
    }

    float s = (-s1.y * (r0.x - a.x) + s1.x * (r0.y - a.y)) / denom;
    float t = ( s2.x * (r0.y - a.y) - s2.y * (r0.x - a.x)) / denom;

    // Check if s and t are within the valid range [0,1] for line segments
    if (s >= 0.0 && s <= 1.0 && t >= 0.0 && t <= 1.0)
    {
        // Intersection detected
        float2 intersectionPoint = r0 + t * s1;
        return intersectionPoint;
    }
    else
    {
        // No intersection within the line segments
        return float2(1.#INF, 1.#INF);
    }
}

float2 DstToLineSegment(float2 A, float2 B, float2 P)
{
    float2 AB = B - A;
    float2 AP = P - A;
    float ABLengthSquared = dot2(AB);
    if (ABLengthSquared == 0.0)
    {
        // If A == B, return the vector from P to A
        return A - P;
    }

    // Scalar projection
    float AP_dot_AB = dot(AP, AB);
    float t = AP_dot_AB / ABLengthSquared;

    // Clamp t to the closest P on the line segment
    t = clamp(t, 0.0, 1.0);

    // Closest P on line segment to P
    float2 closestPoint = A + t * AB;

    // Return the distance vector from P to the closest P
    return closestPoint - P;
}

// Computes the vector cross product of a scalar and a 2D vector (z-component assumed)
float2 crossZ(float z, float2 v)
{
    return z * float2(-v.y, v.x);
}

float RayLineIntersect(float2 pos, float2 dir, float2 A, float2 B)
{
    float2 r = dir;          // Direction vector of the ray
    float2 s = B - A;        // Direction vector of the line segment
    float2 qp = A - pos;     // Vector from ray origin to segment start point

    float r_cross_s = cross2D(r, s);
    float qp_cross_r = cross2D(qp, r);

    // Check if lines are parallel (r_cross_s == 0)
    if (abs(r_cross_s) < EPSILON)
    {
        return 1.#INF; // No intersection, lines are parallel
    }

    float t = cross2D(qp, s) / r_cross_s; // Distance along the ray
    float u = cross2D(qp, r) / r_cross_s; // Parameter along the segment

    // Check if intersection occurs within the ray and the segment
    if (t >= 0 && u >= 0 && u <= 1)
    {
        return t; // Distance to intersection point along the ray
    }

    return 1.#INF; // No valid intersection
}

bool SideOfLine(float2 A, float2 B, float2 dstVec) {
    float2 lineVec = normalize(B - A);

    float crossProduct = cross2D(lineVec, dstVec);

    // True if P is on the left side of the line from A to B
    // This means all lines only "block" one side
    return crossProduct > 0;
}

uint wrapUint(uint a, uint start, uint end)
{
    return start + (a - start) % (end - start);
}

void EnsureNonZero(inout float2 a)
{
    a.x = (a.x > 0 ? 1 : -1) * max(abs(a.x), EPSILON);
    a.y = (a.y > 0 ? 1 : -1) * max(abs(a.y), EPSILON);
}

float2 rotate(float2 P, float angle)
{
    float cosTheta = cos(angle);
    float sinTheta = sin(angle);

    return float2(P.x * cosTheta - P.y * sinTheta,
                    P.x * sinTheta + P.y * cosTheta);
}

bool AreAllComponentsEqualTo(float3 a, float b)
{
    return a.x == b && a.y == b && a.z == b;
}
bool AreAllComponentsEqualTo(float2 a, float b)
{
    return a.x == b && a.y == b;
}
bool IsAnyComponentEqualTo(float3 a, float b)
{
    return a.x == b || a.y == b || a.z == b;
}
bool IsAnyComponentEqualTo(float2 a, float b)
{
    return a.x == b || a.y == b;
}
bool IsAnyComponentEqualTo(int3 a, int b)
{
    return a.x == b || a.y == b || a.z == b;
}
bool IsAnyComponentEqualTo(int2 a, int b)
{
    return a.x == b || a.y == b;
}

// Bit manipulation helpers
void SetBit(inout int value, int bitIndex, bool state)
{
    if (state)
        value |= (1 << bitIndex); // Set the bit
    else
        value &= ~(1 << bitIndex); // Clear the bit
}
bool ReadBit(int value, int bitIndex)
{
    return (value & (1 << bitIndex)) != 0;
}