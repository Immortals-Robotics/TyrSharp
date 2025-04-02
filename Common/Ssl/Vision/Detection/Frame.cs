using ProtoBuf;

namespace Tyr.Common.Ssl.Vision.Detection;

[ProtoContract]
public class Frame
{
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }

    [ProtoMember(2, IsRequired = true)] public double CaptureTime { get; set; }
    [ProtoMember(3, IsRequired = true)] public double SentTime { get; set; }
    [ProtoMember(8)] public double? CameraCaptureTime { get; set; }

    [ProtoMember(4, IsRequired = true)] public uint CameraId { get; set; }

    [ProtoMember(5)] public List<Ball> Balls { get; set; } = [];

    [ProtoMember(6)] public List<Robot> RobotsYellow { get; set; } = [];

    [ProtoMember(7)] public List<Robot> RobotsBlue { get; set; } = [];
}