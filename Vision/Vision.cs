using Tyr.Common.Config;
using Tyr.Common.Time;

namespace Tyr.Vision;

[Configurable]
public sealed class Vision
{
    [ConfigEntry] private static float CameraTooOldTime { get; set; } = 1f;

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
        IEnumerable<DetectionFrame> frames,
        IEnumerable<CameraCalibration> calibrations,
        FieldSize? fieldSize)
    {
        foreach (var calibration in calibrations)
        {
            var camera = GetOrCreateCamera(calibration.CameraId);
            camera.OnCalibration(calibration);
        }

        if (fieldSize.HasValue)
        {
            foreach (var camera in _cameras.Values)
                camera.OnFieldSize(fieldSize.Value);
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
                camera.Timestamp - averageTimestamp > DeltaTime.FromSeconds(CameraTooOldTime));
        }
    }
}