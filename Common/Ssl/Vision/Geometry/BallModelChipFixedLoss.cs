using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Geometry;

[ProtoContract]
public class BallModelChipFixedLoss
{
    [ProtoMember(1, IsRequired = true)] public double DampingXYFirstHop { get; set; }
    [ProtoMember(2, IsRequired = true)] public double DampingXYOtherHops { get; set; }
    [ProtoMember(3, IsRequired = true)] public double DampingZ { get; set; }
}