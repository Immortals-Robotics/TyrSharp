using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class MultipleCards
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}