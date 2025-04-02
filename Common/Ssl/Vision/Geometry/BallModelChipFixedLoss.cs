using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Geometry;

[ProtoContract]
public struct BallModelChipFixedLoss
{
    [ProtoMember(1, IsRequired = true)] public double DampingXyFirstHop { get; set; }
    [ProtoMember(2, IsRequired = true)] public double DampingXyOtherHops { get; set; }
    [ProtoMember(3, IsRequired = true)] public double DampingZ { get; set; }
}