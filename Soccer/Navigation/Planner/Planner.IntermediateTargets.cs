using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;

namespace Tyr.Soccer.Navigation.Planner;

public partial class Planner
{
    [ConfigEntry] private static float MinRadius { get; set; } = 100f;
    [ConfigEntry] private static float RadiusStep { get; set; } = 1000f;
    [ConfigEntry] private static int RadiusStepCount { get; set; } = 2;

    [ConfigEntry] private static int AngleStepCount { get; set; } = 16;

    // TODO: make this an IEunumerator
    private List<Vector2> GenerateIntermediateTargetsSystematic(Vector2 center)
    {
        var angleStep = 360f / AngleStepCount;

        var rndRadiusOffset = _random.Get(0f, RadiusStep);

        var results = new List<Vector2>(RadiusStepCount * AngleStepCount);

        for (var i = 0; i <= RadiusStepCount; i++)
        {
            var radius = MinRadius + RadiusStep * i + rndRadiusOffset;

            var rndAngleOffset = _random.Get(0f, angleStep);

            for (var j = 0; j < AngleStepCount; j++)
            {
                var angleDeg = j * angleStep + rndAngleOffset;
                var angle = Angle.FromDeg(angleDeg);
                results.Add(center.CircleAroundPoint(angle, radius));
            }
        }

        return results;
    }
}