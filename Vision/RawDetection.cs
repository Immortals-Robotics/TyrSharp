using Tyr.Common.Data.Ssl.Vision.Detection;

namespace Tyr.Vision;

/// <summary>
/// Represents a raw detection (robot or ball) from a single detection frame,
/// annotated with its source camera and timestamps.
/// </summary>
public readonly record struct RawDetection<T> where T : IObject
{
    public T Detection { get; init; }

    public uint CameraId { get; init; }
    public uint FrameNumber { get; init; }

    public DateTime CaptureTime { get; init; }
    public DateTime SentTime { get; init; }
    public DateTime? CameraCaptureTime { get; init; }

    public bool IsBall => typeof(T) == typeof(Ball);
    public bool IsRobot => typeof(T) == typeof(Robot);

    public RawDetection(T detection, Frame frame)
    {
        Detection = detection;

        CameraId = frame.CameraId;
        FrameNumber = frame.FrameNumber;

        CaptureTime = frame.CaptureTime;
        SentTime = frame.SentTime;
        CameraCaptureTime = frame.CameraCaptureTime;
    }

    public override string ToString()
    {
        var type = IsBall ? "Ball" : IsRobot ? "Robot" : typeof(T).Name;
        return $"{type} from cam {CameraId} frame {FrameNumber} at {CaptureTime:HH:mm:ss.fff}";
    }
}