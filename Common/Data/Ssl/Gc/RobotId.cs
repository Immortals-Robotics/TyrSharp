using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Gc;

/// <summary>
/// RobotId is the combination of a team and a robot id.
/// </summary>
[ProtoContract]
public struct RobotId
{
    /// <summary> The robot number. </summary>
    [ProtoMember(1)] public uint? Id { get; set; }

    /// <summary> The team that the robot belongs to. </summary>
    [ProtoMember(2)] public TeamColor? Team { get; set; }
}
