using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Soccer.Navigation.Planner;

public partial class Planner
{
    [ConfigEntry] private static float AcceptableFreeDistance { get; set; } = 50f;
    [ConfigEntry] private static int NearestFreeIterations { get; set; } = 1000;

    private Vector2 NearestFree(Vector2 state, float margin)
    {
        // clamp the position to be inside the field
        var fieldMargin = Context.Field.BoundaryWidth - Context.RobotRadius;
        var maxX = Context.Field.Width + fieldMargin;
        var maxY = Context.Field.Height + fieldMargin;

        state.X = MathF.Abs(state.X) > maxX ? MathF.Sign(state.X) * maxX : state.X;
        state.Y = MathF.Abs(state.Y) > maxY ? MathF.Sign(state.Y) * maxY : state.Y;

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

        for (var i = 0; i < NearestFreeIterations; i++)
        {
            var rnd = RandomState();
            var distSq = Vector2.DistanceSquared(state, rnd);

            if (Map.Inside(rnd, margin) || distSq >= minDistSq) continue;

            result = rnd;
            minDistSq = distSq;

            if (minDistSq < acceptableFreeDistanceSq)
                break;
        }

        _lastNearestFree = result;
        return result;
    }

    private Vector2 RandomState()
    {
        var margin = Context.Field.BoundaryWidth - Context.RobotRadius;

        var x = (_random.Get(-1f, 1f)) * (Context.Field.Width + margin);
        var y = (_random.Get(-1f, 1f)) * (Context.Field.Height + margin);

        return new Vector2(x, y);
    }
}