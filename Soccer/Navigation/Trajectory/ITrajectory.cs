using System.Numerics;

namespace Tyr.Soccer.Navigation.Trajectory;

public interface ITrajectory<out T>
{
    T GetPosition(float t);
    T GetVelocity(float t);
    T GetAcceleration(float t);

    float StartTime { get; }
    float EndTime { get; }
    float Duration { get; }
}
