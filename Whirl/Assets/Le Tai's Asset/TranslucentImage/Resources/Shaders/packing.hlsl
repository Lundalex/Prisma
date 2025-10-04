// Copyright (c) Le Loc Tai <leloctai.com> . All rights reserved. Do not redistribute.

#ifndef LETAI_PACKING
#define LETAI_PACKING

struct FloatUnpacker
{
    uint payload;
    uint nBitsConsumed;
    uint bitsPerFloat;
};

FloatUnpacker CreateUnpacker(float packed, int bitsPerFloat = 0)
{
    uint bits = asuint(packed);
    uint exponent = (bits >> 23) & 0xFFu;
    uint mantissa = bits & 0x7FFFFFu;

    FloatUnpacker u;
    u.payload = ((exponent - 1u) << 23) | mantissa;
    u.nBitsConsumed = 0;
    u.bitsPerFloat = bitsPerFloat;
    return u;
}

float Dequeue(inout FloatUnpacker u, float minVal, float maxVal, uint nBits = 0)
{
    if (nBits == 0) nBits = u.bitsPerFloat;

    uint mask = (1u << nBits) - 1u;
    uint value = (u.payload >> u.nBitsConsumed) & mask;
    u.nBitsConsumed += nBits;

    float norm = value * (1.0 / mask);
    return norm * (maxVal - minVal) + minVal;
}

float DequeueNonNegative(inout FloatUnpacker u, float maxVal, uint nBits = 0)
{
    return Dequeue(u, 0, maxVal, nBits);
}


void UnpackTwoFloatsSafe(
    float     packed,
    float     minA, float  maxA, uint nBitsA,
    float     minB, float  maxB, uint nBitsB,
    out float a, out float b)
{
    uint bits = asuint(packed);

    uint exponent = (bits >> 23) & 0xFFu;
    uint mantissa = bits & 0x7FFFFFu;

    uint payload = ((exponent - 1u) << 23) | mantissa;

    uint  maskB = (1u << nBitsB) - 1u;
    float aQuantized = payload >> nBitsB;
    float bQuantized = payload & maskB;

    float maxAQuantized = (1u << nBitsA) - 1u;
    float maxBQuantized = maskB;

    float aNormalized = aQuantized * (1. / maxAQuantized);
    float bNormalized = bQuantized * (1. / maxBQuantized);

    a = aNormalized * (maxA - minA) + minA;
    b = bNormalized * (maxB - minB) + minB;
}

#endif
