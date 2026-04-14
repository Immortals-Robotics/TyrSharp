using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct SimulatorResponse
{
    [ProtoMember(1)] public List<Error> Errors { get; set; }
}
