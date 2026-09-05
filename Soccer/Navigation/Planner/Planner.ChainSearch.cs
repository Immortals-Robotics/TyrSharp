using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Navigation.Planner;

public partial class Planner
{
    [ConfigEntry] private static partial float ChainSearchTimeStep { get; set; } = 0.2f;

    // finds a trajectory that consists of
    // 1- init to time t1 somewhere on the input trajectory
    // 2- trajectory from the state at t1 to target
    private Trajectory2DChained FindChainedTrajectory(Trajectory2D trajectory)
    {
        var rndOffset = _random.Get(0f, ChainSearchTimeStep);

        var tStart = trajectory.StartTime + rndOffset;
        var tEnd = MathF.Min(trajectory.StartTime + LookaheadTime, trajectory.EndTime);
        var lastPos = trajectory.GetPosition(trajectory.StartTime);
        var prefixCollision = false;

        for (var t = tStart; t < tEnd; t += ChainSearchTimeStep)
        {
            var pos = trajectory.GetPosition(t);
            var vel = trajectory.GetVelocity(t);

            if (!prefixCollision)
            {
                prefixCollision = Map.CollisionDetect(new LineSegment
                {
                    Start = lastPos,
                    End = pos,
                });
            }

            var second = TrajectoryBangBang.Make2D(pos, vel, _target, Profile);

            if (!Map.HasCollision(second).collided || prefixCollision)
                return new Trajectory2DChained()
                {
                    First = trajectory,
                    Second = second,
                    CutTime = t,
                };

            lastPos = pos;
        }

        var posEnd = trajectory.GetPosition(tEnd);
        var velEnd = trajectory.GetVelocity(tEnd);

        var targetTrajectory = TrajectoryBangBang.Make2D(posEnd, velEnd, _target, Profile);
        return new Trajectory2DChained()
        {
            First = trajectory,
            Second = targetTrajectory,
            CutTime = tEnd,
        };
    }
}
