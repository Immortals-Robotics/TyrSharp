using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct SimulatorControl
{
    [ProtoMember(1)] public TeleportBall? TeleportBall { get; set; }
    [ProtoMember(2)] public List<TeleportRobot> TeleportRobot { get; set; }

    [ProtoMember(3)] public float? SimulationSpeed { get; set; }
}
