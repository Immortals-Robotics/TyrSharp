using System.Numerics;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Navigation.Planner;

public partial class Planner
{
    private float CalculateTrajectoryPenalty(TrajectoryChained trajectory)
    {
        var penalty = 0f;

        var collision = Map.HasCollision(trajectory, LookaheadTime);
        penalty += trajectory.Duration;

        if (collision.collided)
        {
            penalty += 5.0f;
            penalty += MathF.Max(0f, LookaheadTime - collision.time);
        }

        var free = Map.ReachFree(trajectory, LookaheadTime);
        penalty += 3f * free.time;

        // TODO: this biases towards shorter paths
        if (trajectory.Duration > LookaheadTime)
        {
            var posAtLookahead = trajectory.GetPosition(LookaheadTime);
            penalty += Vector2.Distance(posAtLookahead, _target) / 1000f;
        }

        return penalty;
    }
}