namespace Tyr.Common.Vision.Data;

public readonly record struct FilteredBall
{
    public Timestamp Timestamp { get; init; }
    public Timestamp LastVisibleTimestamp { get; init; }
    public BallState State { get; init; }

    public BallTouchdown? GetNextTouchdown()
    {
        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(State);
        return trajectory is IBallTouchdownTrajectory touchdownTrajectory
            ? touchdownTrajectory.GetNextTouchdown()
            : null;
    }

    public FilteredBall Extrapolate(Timestamp timestamp)
    {
        if (timestamp <= Timestamp) return this;

        var dt = timestamp - Timestamp;

        return new FilteredBall
        {
            Timestamp = timestamp,
            LastVisibleTimestamp =
                LastVisibleTimestamp +
                dt,
            State = ServiceLocator.BallTrajectoryFactory.GetStateAt(State, dt)
        };
    }
}
