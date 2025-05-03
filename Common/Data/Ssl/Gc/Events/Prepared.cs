using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

[ProtoContract]
public class Prepared
{
    [ProtoMember(1)] public float? TimeTakenSeconds { get; set; }

    public DeltaTime? TimeTaken => TimeTakenSeconds.HasValue
        ? DeltaTime.FromSeconds(TimeTakenSeconds.Value)
        : null;
}