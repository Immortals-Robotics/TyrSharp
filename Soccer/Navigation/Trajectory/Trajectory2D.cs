using System.Numerics;

namespace Tyr.Soccer.Navigation.Trajectory;

public readonly struct Trajectory2D
{
    public Trajectory1DPieced TrajectoryX { get; init; }
    public Trajectory1DPieced TrajectoryY { get; init; }

    public Vector2 GetPosition(float t)
    {
        return new Vector2(TrajectoryX.GetPosition(t), TrajectoryY.GetPosition(t));
    }

    public Vector2 GetVelocity(float t)
    {
        return new Vector2(TrajectoryX.GetVelocity(t), TrajectoryY.GetVelocity(t));
    }

    public Vector2 GetAcceleration(float t)
    {
        return new Vector2(TrajectoryX.GetAcceleration(t), TrajectoryY.GetAcceleration(t));
    }

    public float StartTime => Math.Max(TrajectoryX.StartTime, TrajectoryY.StartTime);
    public float EndTime => Math.Min(TrajectoryX.EndTime, TrajectoryY.EndTime);
    public float Duration => Math.Max(0.0f, EndTime - StartTime);
}
