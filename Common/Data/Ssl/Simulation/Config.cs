using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct Config
{
    [ProtoMember(1)] public Vision.Geometry.Data? Geometry { get; set; }

    [ProtoMember(2)] public List<RobotSpecs> RobotSpecs { get; set; }

    [ProtoMember(3)] public RealismConfig? RealismConfig { get; set; }

    [ProtoMember(4)] public uint? VisionPort { get; set; }
}