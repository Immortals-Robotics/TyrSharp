using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Time;
using Tyr.Vision.Data;

namespace Tyr.Vision;

[Configurable]
public sealed partial class Vision
{
    [ConfigEntry] private static DeltaTime CameraTooOldTime { get; set; } = DeltaTime.FromSeconds(1f);

    public static FieldSize FieldSize { get; set; } = FieldSize.DivisionA;

    private readonly FilteredFrame _filteredFrame = new();

    private readonly Dictionary<uint, Camera> _cameras = [];

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
        IEnumerable<Detection.Frame> frames,
        IEnumerable<CameraCalibration> calibrations)
    {
        foreach (var calibration in calibrations)
        {
            var camera = GetOrCreateCamera(calibration.CameraId);
            camera.Calibration = calibration;
        }

        foreach (var frame in frames)
        {
            var camera = GetOrCreateCamera(frame.CameraId);
            camera.OnFrame(frame, _filteredFrame);
        }

        // remove old cameras
        if (_cameras.Count > 0)
        {
            var averageTimestamp = Timestamp.FromSeconds(
                _cameras.Values.Average(camera => camera.Timestamp.Seconds));
            _cameras.RemoveAll((_, camera) =>
                camera.Timestamp - averageTimestamp > CameraTooOldTime);
        }

        foreach (var camera in _cameras.Values)
        {
            Log.ZLogTrace($"Camera {camera.Id} FPS: {camera.Fps:F2}");
            Plot.Plot($"cam[{camera.Id}] fps", camera.Fps, "fps");

            DrawRobots(camera);
            DrawBalls(camera);
        }
    }

    private static void DrawBalls(Camera camera)
    {
        for (var index = 0; index < camera.Balls.Count; index++)
        {
            var tracker = camera.Balls[index];
            Draw.DrawCircle(tracker.Filter.Position, 25f, Color.Orange400,
                Options.Filled with { Thickness = 5f });

            Plot.Plot($"cam[{camera.Id}] ball[{index}]", tracker.Filter.Velocity, "vel (mm/s)");
        }
    }

    private static void DrawRobots(Camera camera)
    {
        foreach (var (id, tracker) in camera.Robots)
        {
            Draw.DrawRobot(tracker.Position, tracker.Angle, id,
                Options.Filled with { Thickness = 10f });

            Plot.Plot($"cam[{camera.Id}] robot[{id}]", tracker.Velocity, "vel (mm/s)");
        }
    }
}