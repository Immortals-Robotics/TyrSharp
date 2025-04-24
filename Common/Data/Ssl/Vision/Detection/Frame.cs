using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

[ProtoContract]
public class Frame
{
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }

    [ProtoMember(2, IsRequired = true)] public double CaptureTimeSeconds { get; set; }
    public Timestamp CaptureTime => Timestamp.FromSeconds(CaptureTimeSeconds);

    [ProtoMember(3, IsRequired = true)] public double SentTimeSeconds { get; set; }
    public Timestamp SentTime => Timestamp.FromSeconds(SentTimeSeconds);

    [ProtoMember(8)] public double? CameraCaptureTimeSeconds { get; set; }

    public Timestamp? CameraCaptureTime => CameraCaptureTimeSeconds.HasValue
        ? Timestamp.FromSeconds(CameraCaptureTimeSeconds.Value)
        : null;

    [ProtoMember(4, IsRequired = true)] public uint CameraId { get; set; }

    [ProtoMember(5)] public List<Ball> Balls { get; set; } = [];

    [ProtoMember(6)] public List<Robot> RobotsYellow { get; set; } = [];

    [ProtoMember(7)] public List<Robot> RobotsBlue { get; set; } = [];
}