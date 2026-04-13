namespace Tyr.Soccer.Navigation.Trajectory;

public readonly struct Trajectory1DConstantAcc
{
    public float Acceleration { get; init; }
    public float StartVelocity { get; init; }
    public float StartPosition { get; init; }

    public float StartTime { get; init; }
    public float EndTime { get; init; }

    public readonly float GetPosition(float t)
    {
        var dt = t - StartTime;
        return StartPosition + StartVelocity * dt + Acceleration * (dt * dt * .5f);
    }

    public readonly float GetVelocity(float t)
    {
        var dt = t - StartTime;
        return StartVelocity + Acceleration * dt;
    }

    public readonly float GetAcceleration(float t) => Acceleration;

    public float Duration => Math.Max(0.0f, EndTime - StartTime);
}
