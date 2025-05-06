using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Common.Vision.Data;

public readonly record struct RobotState
{
    /// <summary>
    /// Position in millimeters [mm]
    /// </summary>
    public Vector2 Position { get; init; }

    /// <summary> 
    /// Velocity in millimeters per second [mm/s]
    /// </summary>
    public Vector2 Velocity { get; init; }

    public Angle Angle { get; init; }

    public Angle AngularVelocity { get; init; }
}