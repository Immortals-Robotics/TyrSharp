using Tyr.Common.Config;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Navigation.Planner;

public partial class Planner
{
    [ConfigEntry] private static float ChainSearchTimeStep { get; set; } = 0.2f;

    // finds a trajectory that consists of
    // 1- init to time t1 somewhere on the input trajectory
    // 2- trajectory from the state at t1 to target
    private TrajectoryChained FindChainedTrajectory(Trajectory2D trajectory)
    {
        var rndOffset = _random.Get(0f, ChainSearchTimeStep);

        var tStart = trajectory.StartTime + rndOffset;
        var tEnd = MathF.Min(trajectory.StartTime + LookaheadTime, trajectory.EndTime);

        for (var t = tStart; t < tEnd; t += ChainSearchTimeStep)
        {
            var pos = trajectory.GetPosition(t);
            var vel = trajectory.GetVelocity(t);
            
            var second = TrajectoryBangBang.Make2D(pos, vel, _target, Profile);

            if (!Map.HasCollision(second).collided || Map.HasCollision(trajectory, t).collided)
                return new TrajectoryChained()
                {
                    First = trajectory,
                    Second = second,
                    CutTime = t,
                };
        }

        var posEnd = trajectory.GetPosition(tEnd);
        var velEnd = trajectory.GetVelocity(tEnd);

        var targetTrajectory = TrajectoryBangBang.Make2D(posEnd, velEnd, _target, Profile);
        return new TrajectoryChained()
        {
            First = trajectory,
            Second = targetTrajectory,
            CutTime = tEnd,
        };
    }
}