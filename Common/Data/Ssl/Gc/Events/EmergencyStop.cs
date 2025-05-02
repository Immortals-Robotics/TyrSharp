using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc.Events;

/// An emergency stop, requested by team previously, occurred
[ProtoContract]
public class EmergencyStop
{
    /// The team that substitutes robots
    [ProtoMember(1, IsRequired = true)] public TeamColor ByTeam { get; set; }
}