using System.Numerics;

namespace Tyr.Tests.Common.Math;

internal static class LineTestData
{
    public static List<Vector2> CreateNoisySamples(
        Vector2 pointOnLine,
        Vector2 direction,
        int count,
        float spacing,
        float tangentNoiseStdDev,
        float normalNoiseStdDev,
        int seed)
    {
        var rng = new Random(seed);
        var normalizedDirection = Vector2.Normalize(direction);
        var normal = new Vector2(-normalizedDirection.Y, normalizedDirection.X);
        var start = -(count - 1) * 0.5f;
        var samples = new List<Vector2>(count);

        for (var i = 0; i < count; i++)
        {
            var along = (start + i) * spacing + NextGaussian(rng, tangentNoiseStdDev);
            var perpendicular = NextGaussian(rng, normalNoiseStdDev);
            samples.Add(pointOnLine + normalizedDirection * along + normal * perpendicular);
        }

        return samples;
    }

    public static List<Vector2> CreateNoisySlopeInterceptSamples(
        float slope,
        float intercept,
        int count,
        float xStart,
        float xStep,
        float yNoiseStdDev,
        int seed)
    {
        var rng = new Random(seed);
        var samples = new List<Vector2>(count);

        for (var i = 0; i < count; i++)
        {
            var x = xStart + i * xStep;
            var y = slope * x + intercept + NextGaussian(rng, yNoiseStdDev);
            samples.Add(new Vector2(x, y));
        }

        return samples;
    }

    public static float DirectionSimilarity(Vector2 a, Vector2 b) => MathF.Abs(Vector2.Dot(a, b));

    private static float NextGaussian(Random rng, float stdDev)
    {
        var u1 = 1.0 - rng.NextDouble();
        var u2 = 1.0 - rng.NextDouble();
        var radius = System.Math.Sqrt(-2.0 * System.Math.Log(u1));
        var theta = 2.0 * System.Math.PI * u2;
        return (float)(radius * System.Math.Cos(theta) * stdDev);
    }
}
