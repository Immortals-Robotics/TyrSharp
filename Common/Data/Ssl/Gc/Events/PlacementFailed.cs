using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class PlacementFailed
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    [ProtoMember(2)] public float? RemainingDistance { get; set; }
    [ProtoMember(3)] public float? NearestOwnBotDistance { get; set; }
}