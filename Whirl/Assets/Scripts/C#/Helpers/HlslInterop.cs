using System.Runtime.InteropServices;

public static class HlslInterop
{
    [StructLayout(LayoutKind.Explicit)]
    private struct FloatUintUnion
    {
        [FieldOffset(0)] public float FloatValue;
        [FieldOffset(0)] public uint UintValue;
    }

    public static uint FloatToUint(float value)
    {
        FloatUintUnion union = new() { FloatValue = value };
        return union.UintValue;
    }

    public static float UintToFloat(uint value)
    {
        FloatUintUnion union = new() { UintValue = value };
        return union.FloatValue;
    }
}