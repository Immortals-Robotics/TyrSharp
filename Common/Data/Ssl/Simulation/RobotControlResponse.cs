using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Simulation;

[ProtoContract]
public struct RobotControlResponse
{
    [ProtoMember(1)] public List<Error> Errors { get; set; }
    [ProtoMember(2)] public List<RobotFeedback> Feedback { get; set; }
}