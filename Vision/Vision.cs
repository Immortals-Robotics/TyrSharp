using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Tracking;

namespace Tyr.Vision;

[Configurable]
public sealed partial class Vision
{
    [ConfigEntry] private static DeltaTime CameraTooOldTime { get; set; } = DeltaTime.FromSeconds(1f);

    public static FieldSize FieldSize { get; set; } = FieldSize.DivisionA;

    private FilteredFrame _lastFilteredFrame = new();

    private readonly Dictionary<uint, Camera> _cameras = [];

    private readonly BallMerger _ballMerger = new();
    private readonly RobotMerger _robotMerger = new();

    private Camera GetOrCreateCamera(uint id)
    {
        if (_cameras.TryGetValue(id, out var camera)) return camera;

        camera = new Camera(id);
        _cameras.Add(id, camera);

        return camera;
    }

    /// Processes the latest batch of detection frames, calibrations, and field size.
    /// This method should be called once per tick.
    internal void Process(
        IReadOnlyList<Detection.Frame> frames,
        IReadOnlyList<CameraCalibration> calibrations)
    {
        foreach (var calibration in calibrations)
        {
            var camera = GetOrCreateCamera(calibration.CameraId);
            camera.Calibration = calibration;
        }

        foreach (var detection in frames)
        {
            var camera = GetOrCreateCamera(detection.CameraId);
            camera.OnFrame(detection, _lastFilteredFrame);
        }

        // remove old cameras
        if (_cameras.Count > 0)
        {
            var averageTimestamp = Timestamp.FromSeconds(
                _cameras.Values.Average(camera => camera.Timestamp.Seconds));
            _cameras.RemoveAll((_, camera) =>
                camera.Timestamp - averageTimestamp > CameraTooOldTime);
        }

        var frame = GenerateFilteredFrame();
        Hub.Vision.Publish(frame);

        foreach (var camera in _cameras.Values)
        {
            camera.DrawDebug();
        }

        DrawFilteredFrame(frame);

        _lastFilteredFrame = frame;
    }

    private FilteredFrame GenerateFilteredFrame()
    {
        var timestamp = _cameras.Count > 0
            ? _cameras.Values.Select(camera => camera.Timestamp).Max()
            : Timestamp.Zero;

        // prevent negative delta time
        timestamp = Timestamp.Max(_lastFilteredFrame.Timestamp, timestamp);

        var robots = _robotMerger.Process(_cameras.Values, timestamp);

        var mergedBall = _ballMerger.Process(_cameras.Values, timestamp, _lastFilteredFrame.Ball);
        FilteredBall ball;
        if (mergedBall != null)
        {
            ball = new FilteredBall()
            {
                Timestamp = timestamp,
                State = new BallState()
                {
                    Position = mergedBall.Value.Position.Xyz(),
                    Velocity = mergedBall.Value.Velocity.Xyz(),
                    Acceleration = mergedBall.Value.Velocity.WithLength(-BallParameters.AccelerationRoll).Xyz(),
                    SpinRadians = mergedBall.Value.Velocity / BallParameters.Radius
                },
                LastVisibleTimestamp = mergedBall.Value.LatestRawBall?.CaptureTimestamp ??
                                       _lastFilteredFrame.Ball.LastVisibleTimestamp,
            };
        }
        else
        {
            ball = _lastFilteredFrame.Ball with
            {
                Timestamp = timestamp,
                State = _lastFilteredFrame.Ball.State with
                {
                    Velocity = Vector3.Zero,
                    Acceleration = Vector3.Zero,
                    SpinRadians = Vector2.Zero,
                },
            };
        }

        var frame = new FilteredFrame()
        {
            Id = _lastFilteredFrame.Id + 1,
            Timestamp = timestamp,
            Ball = ball,
            Robots = robots,
        };
        return frame;
    }
}