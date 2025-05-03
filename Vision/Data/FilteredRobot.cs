using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Vision;

namespace Tyr.Vision.Data;

public readonly record struct FilteredRobot
{
    public RobotId Id { get; init; }

    public Timestamp Timestamp { get; init; }

    public RobotState State { get; init; }

    /// <summary>
    /// Quality value between 0 and 1
    /// </summary>
    public float Quality { get; init; }

    public FilteredRobot Extrapolate(Timestamp timestamp)
    {
        if (timestamp <= Timestamp) return this;

        var dt = timestamp - Timestamp;
        var dtSeconds = (float)dt.Seconds;

        var state = State with
        {
            Position = State.Position + State.Velocity * dtSeconds,
            Angle = State.Angle + State.AngularVelocity * dtSeconds
        };

        return this with
        {
            Timestamp = timestamp,
            State = state
        };
    }
}