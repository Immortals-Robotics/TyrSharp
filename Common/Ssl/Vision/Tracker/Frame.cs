using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Tracker;

[ProtoContract]
public class Frame
{
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }
    [ProtoMember(2, IsRequired = true)] public double Timestamp { get; set; }

    [ProtoMember(3)] public List<Ball> Balls { get; set; } = [];
    [ProtoMember(4)] public List<Robot> Robots { get; set; } = [];

    [ProtoMember(5)] public KickedBall? KickedBall { get; set; }

    [ProtoMember(6)] public List<Capability> Capabilities { get; set; } = [];
}