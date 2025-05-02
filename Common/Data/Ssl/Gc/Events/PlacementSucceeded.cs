using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A team successfully placed the ball
[ProtoContract]
public class PlacementSucceeded
{
    /// The team that did the placement
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The time [s] taken for placing the ball
    [ProtoMember(2)] public float? TimeTakenSeconds { get; set; }

    /// The time taken for placing the ball
    public DeltaTime? TimeTaken => TimeTakenSeconds.HasValue
        ? DeltaTime.FromSeconds(TimeTakenSeconds.Value)
        : null;
    
    /// The distance [m] between placement location and actual ball position
    [ProtoMember(3)] public float? Precision { get; set; }
    
    /// The distance [m] between the initial ball location and the placement position
    [ProtoMember(4)] public float? Distance { get; set; }
}