namespace Tyr.Common.Math.Extensions;

public static class RandomExtensions
{
    public static float Get(this Random random, float min, float max) => random.NextSingle() * (max - min) + min;

    // min is inclusive, max is exclusive
    public static int Get(this Random random, int min, int max) => random.Next(min, max);

    public static T Get<T>(this Random random, IReadOnlyList<T> list) => list[random.Get(0, list.Count)];

    public static T Get<T>(this Random random, Span<T> list) => list[random.Get(0, list.Length)];

    public static T Get<T>(this Random random, ReadOnlySpan<T> list) => list[random.Get(0, list.Length)];

    // This allocates a list from heap on every call
    public static T Get<T>(this Random random, params T[] list) => list[random.Get(0, list.Length)];

    public static T Get<T>(this Random random) where T : struct, Enum => random.Get(Enum.GetValues<T>());

    // Fisher-Yates shuffle for Span<T> (fastest)
    public static void Shuffle<T>(this Random random, Span<T> span)
    {
        for (var i = span.Length - 1; i > 0; i--)
        {
            var j = random.Get(0, i + 1);
            (span[i], span[j]) = (span[j], span[i]);
        }
    }

    // For arrays (uses Span internally)
    public static void Shuffle<T>(this Random random, T[] array) => random.Shuffle(array.AsSpan());

    // For List<T> (in-place swap)
    public static void Shuffle<T>(this Random random, List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Get(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // For generic IList<T> (fallback, not recommended for perf)
    public static void Shuffle<T>(this Random random, IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Get(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}