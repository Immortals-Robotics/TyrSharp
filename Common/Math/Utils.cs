namespace Tyr.Common.Math;

public static class Utils
{
    public static bool ApproximatelyEqual(float a, float b, float epsilon = 0.001f) => MathF.Abs(a - b) <= epsilon;
    public static bool ApproximatelyZero(float a, float epsilon = 0.001f) => ApproximatelyEqual(a, 0f, epsilon);

    public static int SignInt(float value) => value == 0f ? 0 : value > 0f ? 1 : -1;
}