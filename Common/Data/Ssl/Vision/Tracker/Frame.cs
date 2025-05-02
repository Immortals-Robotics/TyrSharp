using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

/// <summary>
/// A frame that contains all currently tracked objects on the field on all cameras.
/// </summary>
[ProtoContract]
public class Frame
{
    /// <summary>
    /// A monotonous increasing frame counter.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }

    /// <summary>
    /// The unix timestamp in [s] of the data.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public double TimestampSeconds { get; set; }
    
    /// <summary>
    /// Timestamp as a Timestamp object.
    /// </summary>
    public Timestamp Timestamp => Timestamp.FromSeconds(TimestampSeconds);

    /// <summary>
    /// The list of detected balls.
    /// The first ball is the primary one.
    /// Sources may add additional balls based on their capabilities.
    /// </summary>
    [ProtoMember(3)] public List<Ball> Balls { get; set; } = [];
    
    /// <summary>
    /// The primary ball, if any balls are detected.
    /// </summary>
    public Ball? Ball => Balls.Count > 0 ? Balls[0] : null;

    /// <summary>
    /// The list of detected robots of both teams.
    /// </summary>
    [ProtoMember(4)] public List<Robot> Robots { get; set; } = [];

    /// <summary>
    /// Information about a kicked ball, if the ball was kicked by a robot and is still moving.
    /// Note: This field is optional. Some source implementations might not set this at any time.
    /// </summary>
    [ProtoMember(5)] public KickedBall? KickedBall { get; set; }

    /// <summary>
    /// List of capabilities of the source implementation.
    /// </summary>
    [ProtoMember(6)] public List<Capability> Capabilities { get; set; } = [];
}