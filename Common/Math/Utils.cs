namespace Tyr.Common.Math;

public static class Utils
{
    public static bool AlmostEqual(float a, float b, float epsilon = 0.001f) => MathF.Abs(a - b) <= epsilon;

    public static int SignInt(float value) => value >= 0 ? 1 : -1;
}