using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct RealismConfig
{
    [ProtoMember(1)] public List<Any>? Custom { get; set; }
}
