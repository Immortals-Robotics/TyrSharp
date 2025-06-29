using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct SyncRequest
{
    [ProtoMember(1)] public float? SimStep { get; set; }

    [ProtoMember(2)] public Command? SimulatorCommand { get; set; }

    [ProtoMember(3)] public RobotControl? RobotControl { get; set; }
}

[ProtoContract]
public struct SyncResponse
{
    [ProtoMember(1)] public List<Vision.Detection.Frame> Detection { get; set; }

    [ProtoMember(2)] public RobotControlResponse? RobotControlResponse { get; set; }
}