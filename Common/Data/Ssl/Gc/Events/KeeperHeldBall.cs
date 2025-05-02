using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A keeper held the ball in its defense area for too long
[ProtoContract]
public class KeeperHeldBall
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The location of the ball [m]
    [ProtoMember(2)] public Vector2? Location { get; set; }
    
    /// The duration [s] that the keeper hold the ball
    [ProtoMember(3)] public float? DurationSeconds { get; set; }

    public DeltaTime? Duration => DurationSeconds.HasValue
        ? DeltaTime.FromSeconds(DurationSeconds.Value)
        : null;
}