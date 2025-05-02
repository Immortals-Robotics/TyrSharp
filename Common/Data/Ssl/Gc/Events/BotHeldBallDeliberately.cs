using System.Numerics;
using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A bot held the ball for too long
[ProtoContract]
public class BotHeldBallDeliberately
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The bot that holds the ball
    [ProtoMember(2)] public uint? ByBot { get; set; }
    
    /// The location of the ball [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
    
    /// The duration [s] that the bot hold the ball
    [ProtoMember(4)] public float? DurationSeconds { get; set; }

    public DeltaTime? Duration => DurationSeconds.HasValue
        ? DeltaTime.FromSeconds(DurationSeconds.Value)
        : null;
}