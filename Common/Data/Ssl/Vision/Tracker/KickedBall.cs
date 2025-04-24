using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public struct KickedBall
{
    [ProtoMember(1, IsRequired = true)] public Vector2 PositionRaw { get; set; }

    public System.Numerics.Vector2 Position
    {
        get => PositionRaw;
        set => PositionRaw = value;
    }

    [ProtoMember(2, IsRequired = true)] public Vector3 VelocityRaw { get; set; }

    public System.Numerics.Vector3 Velocity
    {
        get => VelocityRaw;
        set => VelocityRaw = value;
    }

    [ProtoMember(3, IsRequired = true)] public double StartTimestampSeconds { get; set; }
    public Timestamp StartTimestamp => Timestamp.FromSeconds(StartTimestampSeconds);

    [ProtoMember(4)] public double? StopTimestampSeconds { get; set; }

    public Timestamp? StopTimestamp => StopTimestampSeconds.HasValue
        ? Timestamp.FromSeconds(StopTimestampSeconds.Value)
        : null;

    [ProtoMember(5)] public Vector2? StopPosRaw { get; set; }

    public Vector2? StopPos
    {
        get => StopPosRaw;
        set => StopPosRaw = value;
    }

    [ProtoMember(6)] public RobotId? RobotId { get; set; }
}