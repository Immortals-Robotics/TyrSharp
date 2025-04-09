using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Vision.Tracker;

[ProtoContract]
public class Frame
{
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }

    [ProtoMember(2, IsRequired = true)] public double TimestampSeconds { get; set; }
    public DateTime Timestamp => UnixTime.FromSeconds(TimestampSeconds);

    [ProtoMember(3)] public List<Ball> Balls { get; set; } = [];
    public Ball Ball => Balls.Count > 0 ? Balls[0] : new Ball();


    [ProtoMember(4)] public List<Robot> Robots { get; set; } = [];

    [ProtoMember(5)] public KickedBall? KickedBall { get; set; }

    [ProtoMember(6)] public List<Capability> Capabilities { get; set; } = [];
}