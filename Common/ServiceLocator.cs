using Tyr.Common.Vision;

namespace Tyr.Common;

public static class ServiceLocator
{
    public static IBallTrajectoryFactory BallTrajectoryFactory { get; set; } = null!;
}