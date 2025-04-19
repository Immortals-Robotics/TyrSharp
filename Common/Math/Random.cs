namespace Tyr.Common.Math;

public static class Random
{
    public static float Get(float min, float max)
        => System.Random.Shared.NextSingle() * (max - min) + min;

    public static int Get(int min, int maxInclusive)
        => System.Random.Shared.Next(min, maxInclusive + 1);

    public static T Get<T>(IReadOnlyList<T> list)
        => list[Get(0, list.Count - 1)];

    public static T Get<T>(Span<T> list)
        => list[Get(0, list.Length - 1)];

    // This allocates a list from heap on every call
    public static T Get<T>(params T[] list)
        => list[Get(0, list.Length - 1)];

    public static T Get<T>() where T : struct, Enum
        => Enum.GetValues<T>()[System.Random.Shared.Next(Enum.GetValues<T>().Length)];
}