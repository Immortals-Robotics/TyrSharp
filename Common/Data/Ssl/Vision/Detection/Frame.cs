using ProtoBuf;
using Tyr.Common.Time;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

[ProtoContract]
public class Frame
{
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }

    [ProtoMember(2, IsRequired = true)] public double CaptureTimeSeconds { get; set; }
    public DateTime CaptureTime => UnixTime.FromSeconds(CaptureTimeSeconds);

    [ProtoMember(3, IsRequired = true)] public double SentTimeSeconds { get; set; }
    public DateTime SentTime => UnixTime.FromSeconds(SentTimeSeconds);

    [ProtoMember(8)] public double? CameraCaptureTimeSeconds { get; set; }

    public DateTime? CameraCaptureTime => CameraCaptureTimeSeconds.HasValue
        ? UnixTime.FromSeconds(CameraCaptureTimeSeconds.Value)
        : null;

    [ProtoMember(4, IsRequired = true)] public uint CameraId { get; set; }

    [ProtoMember(5)] public List<Ball> Balls { get; set; } = [];

    [ProtoMember(6)] public List<Robot> RobotsYellow { get; set; } = [];

    [ProtoMember(7)] public List<Robot> RobotsBlue { get; set; } = [];
}