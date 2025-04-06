namespace Tyr.Common.Math;

public static class Random
{
    public static float Get(float min, float max)
        => System.Random.Shared.NextSingle() * (max - min) + min;

    public static int Get(int min, int maxInclusive)
        => System.Random.Shared.Next(min, maxInclusive + 1);
}