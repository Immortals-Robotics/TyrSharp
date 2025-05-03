using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

/// <summary>
/// A single tracked ball in the SSL-Vision system.
/// </summary>
[ProtoContract]
public struct Ball
{
    /// <summary>
    /// The position (x, y, height) [m] in the ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public Vector3 Position { get; set; }

    /// <summary>
    /// The velocity [m/s] in the ssl-vision coordinate system.
    /// </summary>
    [ProtoMember(2)] public Vector3? Velocity { get; set; }

    /// <summary>
    /// The visibility of the ball.
    /// A value between 0 (not visible) and 1 (visible).
    /// The exact implementation depends on the source software.
    /// </summary>
    [ProtoMember(3)] public float? Visibility { get; set; }
}