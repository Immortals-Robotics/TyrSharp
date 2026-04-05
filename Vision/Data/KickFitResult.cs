using System.Numerics;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;

namespace Tyr.Vision.Data;

public sealed class KickFitResult(
    IReadOnlyList<Vector2> groundProjection,
    double avgDistance,
    IBallTrajectory trajectory,
    Timestamp kickTimestamp,
    string solverName)
{
    public IReadOnlyList<Vector2> GroundProjection { get; init; } = groundProjection;
    public double AvgDistance { get; init; } = avgDistance;
    public IBallTrajectory Trajectory { get; init; } = trajectory;
    public Timestamp KickTimestamp { get; init; } = kickTimestamp;
    public string SolverName { get; init; } = solverName;

    public Vector2 KickPosition => Trajectory.GetState(DeltaTime.Zero).Position;
    public Vector3 KickVelocity => Trajectory.GetState(DeltaTime.Zero).Velocity;

    public BallState GetState(Timestamp timestamp)
    {
        return Trajectory.GetState(timestamp - KickTimestamp);
    }
}
