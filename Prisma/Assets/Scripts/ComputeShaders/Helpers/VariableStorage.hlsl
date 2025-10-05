// Constants for int min and max values
static const int INT_MAX = 2147483646;
static const int INT_MIN = -2147483647;

// Float <-> Int conversions with overflow protection
float IntToFloat(int a, float precision)
{
    return (float)a / precision;
}

float2 Int2ToFloat2(int2 a, float precision)
{
    return float2(IntToFloat(a.x, precision), IntToFloat(a.y, precision));
}

int FloatAsInt(float a, float precision)
{
    // Compute the value to be converted
    float value = a * precision;

    // Clamp the value within the valid int range
    float clampedValue = clamp(value, (float)INT_MIN, (float)INT_MAX);

    return (int)clampedValue;
}

int2 Float2AsInt2(float2 a, float precision)
{
    return int2(FloatAsInt(a.x, precision), FloatAsInt(a.y, precision));
}

int AddFloatToFloatStoredAsInt(int a, float b, float precision)
{
    // Convert 'a' back to float
    float a_float = IntToFloat(a, precision);

    // Compute the sum
    float sum = a_float + b;

    // Convert the sum back to int with overflow protection
    return FloatAsInt(sum, precision);
}

// Rb/fluid-matIndex <-> matIndex conversions with overflow protection
int StoreRBMatIndex(int rbMatIndex)
{
    // Ensure that rbMatIndex is within valid range to prevent underflow
    if (rbMatIndex <= INT_MIN + 1)
    {
        rbMatIndex = INT_MIN + 2; // Adjust to prevent underflow
    }

    return -rbMatIndex - 1;
}

int RetrieveStoredRBMatIndex(int storedRBMatIndex)
{
    // Check for potential underflow
    if (storedRBMatIndex == INT_MIN)
    {
        storedRBMatIndex = INT_MIN + 1; // Adjust to prevent underflow
    }

    return -(storedRBMatIndex + 1);
}

int StoreFluidMatIndex(int fluidMatIndex)
{
    // Ensure that fluidMatIndex is within valid range to prevent overflow
    if (fluidMatIndex >= INT_MAX - 1)
    {
        fluidMatIndex = INT_MAX - 2; // Adjust to prevent overflow
    }

    return fluidMatIndex + 1;
}

int RetrieveStoredFluidMatIndex(int storedMatIndex)
{
    // Check for potential overflow
    if (storedMatIndex <= INT_MIN + 1)
    {
        storedMatIndex = INT_MIN + 2; // Adjust to prevent underflow
    }

    return storedMatIndex - 1;
}
