namespace Tyr.Vision;

/// <summary>
/// Represents a raw detection (robot or ball) from a single detection frame,
/// annotated with its source camera and timestamps.
/// </summary>
public readonly record struct RawDetection<T> where T : Detection.IObject
{
    public T Detection { get; }

    public uint CameraId { get; }
    public uint FrameNumber { get; }

    public Timestamp CaptureTimestamp { get; }
    public Timestamp SentTimestamp { get; }
    public Timestamp? CameraCaptureTimestamp { get; }

    public bool IsBall => typeof(T) == typeof(Detection.Ball);
    public bool IsRobot => typeof(T) == typeof(Detection.Robot);

    public RawDetection(T detection, Detection.Frame frame)
    {
        Detection = detection;

        CameraId = frame.CameraId;
        FrameNumber = frame.FrameNumber;

        CaptureTimestamp = frame.CaptureTime;
        SentTimestamp = frame.SentTime;
        CameraCaptureTimestamp = frame.CameraCaptureTime;
    }

    public override string ToString()
    {
        var type = IsBall ? "Ball" : IsRobot ? "Robot" : typeof(T).Name;
        return $"{type} from cam {CameraId} frame {FrameNumber} at {CaptureTimestamp}";
    }
}