using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A foul for excessive bot substitutions
[ProtoContract]
public class ExcessiveBotSubstitution
{
    /// The team that substitutes robots
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}