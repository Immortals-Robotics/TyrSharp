using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Soccer.Navigation.Planner;

public partial class Planner
{
    [ConfigEntry] private static float AcceptableFreeDistance { get; set; } = 50f;
    [ConfigEntry] private static int NearestFreeIterations { get; set; } = 1000;
    [ConfigEntry] private static float NearestFreeRingSpacing { get; set; } = 120f;

    private Vector2 NearestFree(Vector2 state, float margin)
    {
        state = ClampToField(state);

        if (!Map.Inside(state))
            return state;

        var result = state;
        var minDistSq = float.MaxValue;

        if (_lastNearestFree is { } cached && !Map.Inside(cached, margin))
        {
            result = cached;
            minDistSq = Vector2.DistanceSquared(state, cached);
        }

        var acceptableFreeDistanceSq = AcceptableFreeDistance * AcceptableFreeDistance;

        SearchNearestFreeStructured(state, margin, acceptableFreeDistanceSq, ref result, ref minDistSq);

        if (minDistSq > acceptableFreeDistanceSq)
        {
            for (var i = 0; i < NearestFreeIterations; i++)
            {
                var rnd = RandomState();
                var distSq = Vector2.DistanceSquared(state, rnd);

                if (Map.Inside(rnd, margin) || distSq >= minDistSq)
                    continue;

                result = rnd;
                minDistSq = distSq;

                if (minDistSq < acceptableFreeDistanceSq)
                    break;
            }
        }

        _lastNearestFree = result;
        return result;
    }

    private void SearchNearestFreeStructured(
        Vector2 state,
        float margin,
        float acceptableFreeDistanceSq,
        ref Vector2 result,
        ref float minDistSq)
    {
        var maxRadius = MathF.Max(Context.Field.Width, Context.Field.Height) + Context.Field.BoundaryWidth;
        var spacing = MathF.Max(NearestFreeRingSpacing, margin + Context.RobotRadius);

        for (var radius = spacing; radius <= maxRadius; radius += spacing)
        {
            if (radius * radius >= minDistSq)
                break;

            var circumference = 2f * MathF.PI * radius;
            var samples = Math.Max(8, (int)MathF.Ceiling(circumference / spacing));
            var angleOffset = _random.Get(0f, 2f * MathF.PI);

            for (var i = 0; i < samples; i++)
            {
                var angle = angleOffset + i * 2f * MathF.PI / samples;
                var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * radius;
                var candidate = ClampToField(state + offset);
                var distSq = Vector2.DistanceSquared(state, candidate);

                if (Map.Inside(candidate, margin) || distSq >= minDistSq)
                    continue;

                result = candidate;
                minDistSq = distSq;

                if (minDistSq <= acceptableFreeDistanceSq)
                    return;
            }
        }
    }

    private Vector2 RandomState()
    {
        var margin = Context.Field.BoundaryWidth - Context.RobotRadius;

        var x = (_random.Get(-1f, 1f)) * (Context.Field.Width + margin);
        var y = (_random.Get(-1f, 1f)) * (Context.Field.Height + margin);

        return new Vector2(x, y);
    }

    private static Vector2 ClampToField(Vector2 state)
    {
        var fieldMargin = Context.Field.BoundaryWidth - Context.RobotRadius;
        var maxX = Context.Field.Width + fieldMargin;
        var maxY = Context.Field.Height + fieldMargin;

        state.X = MathF.Abs(state.X) > maxX ? MathF.Sign(state.X) * maxX : state.X;
        state.Y = MathF.Abs(state.Y) > maxY ? MathF.Sign(state.Y) * maxY : state.Y;
        return state;
    }
}
