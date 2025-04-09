using System.Numerics;

namespace Tyr.Common.Time;

public static class Duration
{
    public static float FromMilliseconds<T>(T milliseconds) where T : INumber<T> =>
        float.CreateChecked(milliseconds) / 1000f;

    public static float FromMicroseconds<T>(T microseconds) where T : INumber<T> =>
        float.CreateChecked(microseconds) / 1_000_000f;
}