using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// A team was found guilty for major unsporting behavior
[ProtoContract]
public class UnsportingBehaviorMajor
{
    /// The team that found guilty
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
    
    /// An explanation of the situation and decision
    [ProtoMember(2, IsRequired = true)] public required string Reason { get; set; }
}