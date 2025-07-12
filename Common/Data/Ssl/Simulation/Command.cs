using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct Command
{
    [ProtoMember(1)] public Control? Control { get; set; }
    [ProtoMember(2)] public Config? Config { get; set; }
}