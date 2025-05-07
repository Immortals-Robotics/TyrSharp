using Tyr.Common.Vision;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Trajectory;

public class BallTrajectoryFactory : IBallTrajectoryFactory
{
    public IBallTrajectory Flat(BallState initial)
    {
        return new BallFlat(initial);
    }
}