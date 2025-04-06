using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Geometry;

[ProtoContract]
public struct BallModelStraightTwoPhase
{
    [ProtoMember(1, IsRequired = true)] public double AccSlide { get; set; }
    [ProtoMember(2, IsRequired = true)] public double AccRoll { get; set; }
    [ProtoMember(3, IsRequired = true)] public double KSwitch { get; set; }
}