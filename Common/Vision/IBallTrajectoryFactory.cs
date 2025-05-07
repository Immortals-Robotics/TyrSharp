using Tyr.Common.Vision.Data;

namespace Tyr.Common.Vision;

public interface IBallTrajectoryFactory
{
    public IBallTrajectory Flat(BallState initial);
}