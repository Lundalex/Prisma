uint NextRandom(inout uint state)
{
    state = state * 747796405 + 2891336453;
    uint result = ((state >> ((state >> 28) + 4)) ^ state) * 277803737;
    result = (result >> 22) ^ result;
    return result;
}

float randNormalized(inout uint state)
{
    return NextRandom(state) / 4294967295.0; // 2^32 - 1
}

int randIntSpan(int a, int b, inout uint state)
{
    float randNorm = randNormalized(state);
    int diff = b - a;
    int offset = (int)(diff * randNorm);
    int result = a + offset;
    return result;
}

bool weightedRand(float a, float b, inout uint state)
{
    float randNorm = randNormalized(state);
    
    float relRand = randNorm * b;
    return relRand < a;
}

float randValueNormalDistribution(inout uint state)
{
    float theta = 2 * PI * randNormalized(state);
    float rho = sqrt(-2 * log(randNormalized(state)));
    return rho * cos(theta);
}

float3 randPointOnUnitSphere(inout uint state)
{
    float x = randValueNormalDistribution(state);
    float y = randValueNormalDistribution(state);
    float z = randValueNormalDistribution(state);
    return normalize(float3(x, y, z));
}

float2 randPointInCircle(inout uint state)
{
    float angle = randNormalized(state) * 2 * PI;
    float2 pointOnCircle = float2(cos(angle), sin(angle));
    return pointOnCircle * sqrt(randNormalized(state));
}

float2 randDir(inout uint state)
{
    return normalize(randPointInCircle(state));
}