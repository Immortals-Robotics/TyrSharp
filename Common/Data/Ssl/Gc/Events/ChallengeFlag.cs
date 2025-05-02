using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A challenge flag, requested by a team previously, is flagged
[ProtoContract]
public class ChallengeFlag
{
    /// The team that requested the challenge flag
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}