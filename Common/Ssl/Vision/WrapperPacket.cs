using ProtoBuf;

namespace Tyr.Common.Ssl.Vision;

[ProtoContract]
public class WrapperPacket
{
    [ProtoMember(1)] public Detection.Frame? Detection { get; set; }
    [ProtoMember(2)] public Geometry.Data? Geometry { get; set; }
}