using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct Response
{
    [ProtoMember(1)] public List<Error> Errors { get; set; }
}