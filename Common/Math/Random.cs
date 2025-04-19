namespace Tyr.Common.Math;

public class Random(int? seed = null)
{
    private readonly System.Random _random = seed is null
        ? new System.Random()
        : new System.Random(seed.Value);

    public float Get(float min, float max) => _random.NextSingle() * (max - min) + min;

    // min is inclusive, max is exclusive
    public int Get(int min, int max) => _random.Next(min, max);

    public T Get<T>(IReadOnlyList<T> list) => list[Get(0, list.Count)];

    public T Get<T>(Span<T> list) => list[Get(0, list.Length)];

    public T Get<T>(ReadOnlySpan<T> list) => list[Get(0, list.Length)];

    // This allocates a list from heap on every call
    public T Get<T>(params T[] list) => list[Get(0, list.Length)];

    public T Get<T>() where T : struct, Enum => Get(Enum.GetValues<T>());

    // Fisher-Yates shuffle for Span<T> (fastest)
    public void Shuffle<T>(Span<T> span)
    {
        for (var i = span.Length - 1; i > 0; i--)
        {
            var j = Get(0, i + 1);
            (span[i], span[j]) = (span[j], span[i]);
        }
    }

    // For arrays (uses Span internally)
    public void Shuffle<T>(T[] array)
        => Shuffle(array.AsSpan());

    // For List<T> (in-place swap)
    public void Shuffle<T>(List<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Get(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    // For generic IList<T> (fallback, not recommended for perf)
    public void Shuffle<T>(IList<T> list)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = Get(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}