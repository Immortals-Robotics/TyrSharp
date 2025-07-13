using System.Numerics;

namespace Tyr.Soccer.Navigation.Trajectory;

public record TrajectoryChained : ITrajectory<Vector2>
{
    public required ITrajectory<Vector2> First { get; init; }
    public required ITrajectory<Vector2> Second { get; init; }
    public float CutTime { get; init; }

    public Vector2 GetPosition(float t) =>
        t < CutTime
            ? First.GetPosition(t)
            : Second.GetPosition(t - CutTime);

    public Vector2 GetVelocity(float t) =>
        t < CutTime
            ? First.GetVelocity(t)
            : Second.GetVelocity(t - CutTime);

    public Vector2 GetAcceleration(float t) =>
        t < CutTime
            ? First.GetAcceleration(t)
            : Second.GetAcceleration(t - CutTime);

    public float StartTime => First.StartTime;
    public float EndTime => Second.EndTime + CutTime;
    public float Duration => Math.Max(0f, EndTime - StartTime);
}