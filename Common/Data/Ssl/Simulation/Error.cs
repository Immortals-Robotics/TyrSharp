using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public readonly struct Error
{
    [ProtoMember(1)] public string? Code { get; init; }
    [ProtoMember(2)] public string? Message { get; init; }
}