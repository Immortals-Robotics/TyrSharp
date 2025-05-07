namespace Tyr.Common.Vision.Data;

public readonly record struct FilteredBall
{
    public Timestamp Timestamp { get; init; }
    public Timestamp LastVisibleTimestamp { get; init; }
    public BallState State { get; init; }

    public FilteredBall Extrapolate(Timestamp timestamp)
    {
        if (timestamp <= Timestamp) return this;

        var dt = timestamp - Timestamp;
        var trajectory = ServiceLocator.BallTrajectoryFactory.Flat(State);

        return new FilteredBall
        {
            Timestamp = timestamp,
            LastVisibleTimestamp = LastVisibleTimestamp + dt, // TODO: why + dt?
            State = trajectory.GetState(dt)
        };
    }
}