using Unity.Mathematics;

public struct PData
{
    public float2 predPos;
    public float2 pos;
    public float2 vel;
    public float2 lastVel;
    public float density;
    public float nearDensity;
    public int lastChunkKey_PType_POrder; // composed 3 int structure
    public float temperature; // kelvin
    public float temperatureExchangeBuffer;
    public float recordedPressure;
    public float2 recordedParticleAcc;
    // POrder; // POrder is dynamic, 
    // LastChunkKey; // 0 <= LastChunkKey <= ChunkNum
    // PType; // 0 <= PType <= PTypesNum
}