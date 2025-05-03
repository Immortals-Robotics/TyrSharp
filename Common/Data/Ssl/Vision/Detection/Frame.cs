using ProtoBuf;

namespace Tyr.Common.Data.Ssl.Vision.Detection;

/// <summary>
/// Represents a detection frame from the SSL-Vision system containing ball and robot detections.
/// </summary>
[ProtoContract]
public class Frame
{
    /// <summary>
    /// Monotonously increasing frame number.
    /// </summary>
    [ProtoMember(1, IsRequired = true)] public uint FrameNumber { get; set; }

    /// <summary>
    /// Unix timestamp in [seconds] at which the image has been received by ssl-vision.
    /// </summary>
    [ProtoMember(2, IsRequired = true)] public double CaptureTimeSeconds { get; set; }
    
    /// <summary>
    /// Capture time as a Timestamp object.
    /// </summary>
    public Timestamp CaptureTime => Timestamp.FromSeconds(CaptureTimeSeconds);

    /// <summary>
    /// Unix timestamp in [seconds] at which this message has been sent to the network.
    /// </summary>
    [ProtoMember(3, IsRequired = true)] public double SentTimeSeconds { get; set; }
    
    /// <summary>
    /// Sent time as a Timestamp object.
    /// </summary>
    public Timestamp SentTime => Timestamp.FromSeconds(SentTimeSeconds);

    /// <summary>
    /// Camera timestamp in [seconds] as reported by the camera, if supported.
    /// This is not necessarily a unix timestamp.
    /// </summary>
    [ProtoMember(8)] public double? CameraCaptureTimeSeconds { get; set; }

    /// <summary>
    /// Camera capture time as a Timestamp object, if available.
    /// </summary>
    public Timestamp? CameraCaptureTime => CameraCaptureTimeSeconds.HasValue
        ? Timestamp.FromSeconds(CameraCaptureTimeSeconds.Value)
        : null;

    /// <summary>
    /// Identifier of the camera.
    /// </summary>
    [ProtoMember(4, IsRequired = true)] public uint CameraId { get; set; }

    /// <summary>
    /// Detected balls.
    /// </summary>
    [ProtoMember(5)] public List<Ball> Balls { get; set; } = [];

    /// <summary>
    /// Detected yellow robots.
    /// </summary>
    [ProtoMember(6)] public List<Robot> RobotsYellow { get; set; } = [];

    /// <summary>
    /// Detected blue robots.
    /// </summary>
    [ProtoMember(7)] public List<Robot> RobotsBlue { get; set; } = [];
}