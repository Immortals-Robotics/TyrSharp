using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class ExcessiveBotSubstitution
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}