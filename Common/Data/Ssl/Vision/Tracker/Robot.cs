using System.Numerics;
using ProtoBuf;
using Tyr.Common.Math;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

/// <summary>
/// A single tracked robot in the SSL-Vision system.
/// </summary>
[ProtoContract]
public struct Robot
{
    /// <summary>
    /// The unique identifier of the robot.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public RobotId Id { get; set; }

    /// <summary>
    /// The position [m] in the ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public Vector2 Position { get; set; }

    /// <summary>
    /// The orientation [rad] in the ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public float AngleRad { get; set; }

    /// <summary>
    /// The orientation as an Angle object.
    /// </summary>
    public Angle Angle
    {
        get => Angle.FromRad(AngleRad);
        set => AngleRad = value.Rad;
    }

    /// <summary>
    /// The velocity [m/s] in the ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(4)] public Vector2? Velocity { get; set; }

    /// <summary>
    /// The angular velocity [rad/s] in the ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(5)] public float? AngularVelocityRad { get; set; }

    /// <summary>
    /// The angular velocity as an Angle object, if available.
    /// </summary>
    public Angle? AngularVelocity
    {
        get => AngularVelocityRad.HasValue ? Angle.FromRad(AngularVelocityRad.Value) : null;
        set => AngularVelocityRad = value?.Rad;
    }

    /// <summary>
    /// The visibility of the robot.
    /// A value between 0 (not visible) and 1 (visible).
    /// The exact implementation depends on the source software.
    /// </summary>
    [ProtoMember(6)] public float? Visibility { get; set; }
}