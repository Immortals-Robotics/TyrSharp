using System.Numerics;
using Tyr.Common.Time;

namespace Tyr.Common.Vision.Data;

/// <summary>
/// Interface representing a ball trajectory over time
/// </summary>
public interface IBallTrajectory
{
    /// <summary>
    /// Gets the ball state (position, velocity and acceleration) at a given time
    /// </summary>
    /// <param name="time">The delta time relative to when the state was created to get the state for</param>
    /// <returns>The ball state at the specified time</returns>
    public BallState GetState(DeltaTime time);

    public Vector2 ClosestRollingPoint(Vector2 point);

    public float GetTravelDistance(DeltaTime time);
}
