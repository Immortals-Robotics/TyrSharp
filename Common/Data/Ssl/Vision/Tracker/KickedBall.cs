using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

/// <summary>
/// A ball kicked by a robot, including predictions when the ball will come to a stop.
/// </summary>
[ProtoContract]
public struct KickedBall
{
    /// <summary>
    /// The initial position [m] from which the ball was kicked.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public Vector2 Position { get; set; }

    /// <summary>
    /// The initial velocity [m/s] with which the ball was kicked.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public Vector3 Velocity { get; set; }

    /// <summary>
    /// The unix timestamp [s] when the kick was performed.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public double StartTimestampSeconds { get; set; }
    
    /// <summary>
    /// Start timestamp as a Timestamp object.
    /// </summary>
    public Timestamp StartTimestamp => Timestamp.FromSeconds(StartTimestampSeconds);

    /// <summary>
    /// The predicted unix timestamp [s] when the ball comes to a stop.
    /// </summary>
    [ProtoMember(4)] public double? StopTimestampSeconds { get; set; }

    /// <summary>
    /// Stop timestamp as a Timestamp object, if available.
    /// </summary>
    public Timestamp? StopTimestamp => StopTimestampSeconds.HasValue
        ? Timestamp.FromSeconds(StopTimestampSeconds.Value)
        : null;

    /// <summary>
    /// The predicted position [m] at which the ball will come to a stop.
    /// </summary>
    [ProtoMember(5)] public Vector2? StopPos { get; set; }

    /// <summary>
    /// The robot that kicked the ball.
    /// </summary>
    [ProtoMember(6)] public RobotId? RobotId { get; set; }
}