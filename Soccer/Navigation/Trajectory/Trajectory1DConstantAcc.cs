using MemoryPack;

namespace Tyr.Soccer.Navigation.Trajectory;

[MemoryPackable]
public partial class Trajectory1DConstantAcc : ITrajectory<float>
{
    public float Acceleration { get; init; }
    public float StartVelocity { get; init; }
    public float StartPosition { get; init; }

    public float StartTime { get; set; } = 0f;
    public float EndTime { get; set; } = 0f;

    public float GetPosition(float t)
    {
        var dt = t - StartTime;
        return StartPosition + StartVelocity * dt + Acceleration * (dt * dt * .5f);
    }

    public float GetVelocity(float t)
    {
        var dt = t - StartTime;
        return StartVelocity + Acceleration * dt;
    }

    public float GetAcceleration(float t) => Acceleration;

    public float Duration => Math.Max(0.0f, EndTime - StartTime);
}
