using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct SimulatorCommand
{
    [ProtoMember(1)] public SimulatorControl? Control { get; set; }
    [ProtoMember(2)] public SimulatorConfig? Config { get; set; }
}
