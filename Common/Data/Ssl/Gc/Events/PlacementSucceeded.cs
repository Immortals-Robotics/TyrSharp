using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class PlacementSucceeded
{
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }

    [ProtoMember(2)] public float? TimeTakenSeconds { get; set; }

    public DeltaTime? TimeTaken => TimeTakenSeconds.HasValue
        ? DeltaTime.FromSeconds(TimeTakenSeconds.Value)
        : null;

    [ProtoMember(3)] public float? Precision { get; set; }

    [ProtoMember(4)] public float? Distance { get; set; }
}