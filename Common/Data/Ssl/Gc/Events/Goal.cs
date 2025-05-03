using System.Numerics;
using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A team shot a goal
[ProtoContract]
public class Goal
{
    /// The team that scored the goal
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// The team that shot the goal (different from by_team for own goals)
    [ProtoMember(6)] public TeamColor? KickingTeam { get; set; }
    
    /// The bot that shot the goal
    [ProtoMember(2)] public uint? KickingBot { get; set; }
    
    /// The location where the ball entered the goal [m]
    [ProtoMember(3)] public Vector2? Location { get; set; }
    
    /// The location where the ball was kicked (for deciding if this was a valid goal) [m]
    [ProtoMember(4)] public Vector2? KickLocation { get; set; }
    
    /// The maximum height the ball reached during the goal kick (for deciding if this was a valid goal) [m]
    [ProtoMember(5)] public float? MaxBallHeight { get; set; }
    
    /// Number of robots of scoring team when the ball entered the goal (for deciding if this was a valid goal)
    [ProtoMember(7)] public uint? NumRobotsByTeam { get; set; }
    
    /// The UNIX timestamp [μs] when the scoring team last touched the ball
    [ProtoMember(8)] public ulong? LastTouchByTeam { get; set; }
    
    /// An additional message with e.g. a reason for invalid goals
    [ProtoMember(9)] public string? Message { get; set; }
}