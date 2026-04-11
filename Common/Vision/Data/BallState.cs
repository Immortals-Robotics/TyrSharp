using System.Numerics;
using Tyr.Common.Math;
using MemoryPack;

namespace Tyr.Common.Vision.Data;

[MemoryPackable]
public partial record struct BallState
{
    /// <summary>
    /// The 3D position of the ball in millimeters
    /// </summary>
    public Vector3 Position3D { get; init; }

    [MemoryPackIgnore]
    public Vector2 Position => Position3D.Xy();

    /// <summary>
    /// The 3D velocity of the ball in millimeters per second
    /// </summary> 
    public Vector3 Velocity { get; init; }

    /// <summary>
    /// The 3D acceleration of the ball in millimeters per second squared
    /// </summary>
    public Vector3 Acceleration { get; init; }

    /// <summary>
    /// The ball's rotational spin vector in radians per second around each axis.
    /// Positive spin corresponds to positive linear velocity for each axis.
    /// </summary>
    public Vector2 SpinRadians { get; init; }

    [MemoryPackIgnore]
    public Vector<Angle> Spin => new Vector<Angle>([Angle.FromRad(SpinRadians.X), Angle.FromRad(SpinRadians.Y)]);

    /// <summary>
    /// Returns true if the ball is in a chipped state
    /// by checking if it has any vertical position, velocity or acceleration
    /// </summary>
    [MemoryPackIgnore]
    public bool IsChipped => Position3D.Z > 0 ||
                             !Utils.ApproximatelyZero(Velocity.Z) ||
                             !Utils.ApproximatelyZero(Acceleration.Z);
}