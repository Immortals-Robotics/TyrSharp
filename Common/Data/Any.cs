using ProtoBuf;

namespace Tyr.Common.Data;

[ProtoContract]
public sealed class Any
{
    [ProtoMember(1)] public string? TypeUrl { get; set; }
    [ProtoMember(2)] public byte[]? Value { get; set; }
}
