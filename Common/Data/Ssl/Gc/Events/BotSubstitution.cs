using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// Bots are being substituted by a team
[ProtoContract]
public class BotSubstitution
{
    /// The team that substitutes robots
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}