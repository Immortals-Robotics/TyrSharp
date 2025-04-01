using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Geometry;

[ProtoContract]
public class BallModelStraightTwoPhase
{
    [ProtoMember(1, IsRequired = true)] public double AccSlide { get; set; }
    [ProtoMember(2, IsRequired = true)] public double AccRoll { get; set; }
    [ProtoMember(3, IsRequired = true)] public double KSwitch { get; set; }
}