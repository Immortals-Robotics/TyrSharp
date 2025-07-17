using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Soccer.Navigation.Obstacle;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Navigation.Planner;

[Configurable]
public partial class Planner
{
    [ConfigEntry] private static float LookaheadTime { get; set; } = 3.0f;

    public Map Map { get; set; } = null!;
    public VelocityProfile Profile { get; set; }

    private readonly Random _random = new();

    private Vector2? _intermediateTarget;
    private Vector2? _lastNearestFree;

    private Vector2 _target;

    public Vector2 Plan(Vector2 initialPos, Vector2 initialVel, Vector2 target)
    {
        // if starting in obstacle, just get out first
        if (Map.Inside(initialPos))
        {
            target = NearestFree(initialPos, 100f);
            Map.Physicality = Physicality.Physical;
        }
        else if (Map.Inside(target))
        {
            target = NearestFree(target, 0f);
        }

        _target = target;

        var directTrajectory = TrajectoryBangBang.Make2D(initialPos, initialVel, target, Profile);
        if (!Map.HasCollision(directTrajectory).collided)
        {
            _intermediateTarget = null;
            return target;
        }

        var intermediatePoints = GenerateIntermediateTargetsSystematic(initialPos);

        TrajectoryChained? bestTrajectory = null;
        var minPenalty = float.MaxValue;

        foreach (var point in intermediatePoints)
        {
            // Draw.DrawCircle(point, 50f, Color.Blue200);

            var toPoint = TrajectoryBangBang.Make2D(initialPos, initialVel, point, Profile);

            var chained = FindChainedTrajectory(toPoint);
            var penalty = CalculateTrajectoryPenalty(chained);

            if (penalty >= minPenalty) continue;

            minPenalty = penalty;
            bestTrajectory = chained;
        }

        // check if the last intermediate point is still valid
        if (_intermediateTarget is { } lastTarget)
        {
            var toLast = TrajectoryBangBang.Make2D(initialPos, initialVel, lastTarget, Profile);

            var chained = FindChainedTrajectory(toLast);
            var penalty = CalculateTrajectoryPenalty(chained);

            Log.ZLogDebug($"last penalty: {penalty}, min penalty: {minPenalty}");

            if (penalty < minPenalty + 1.0f)
            {
                minPenalty = penalty;
                bestTrajectory = chained;
            }
        }

        if (bestTrajectory == null)
        {
            Log.ZLogWarning($"Failed to  find trajectory for {initialPos}");
            return target;
        }

        //TrajectoryUtils.DrawTrajectory(bestTrajectory, Color.Blue300);

        var first = bestTrajectory.First;
        var intermediateTarget = first.GetPosition(first.EndTime);

        Draw.DrawCircle(intermediateTarget, 40f, Color.Red400, Options.Filled);

        _intermediateTarget = intermediateTarget;
        return intermediateTarget;
    }
}