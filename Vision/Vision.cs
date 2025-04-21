using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;

namespace Tyr.Vision;

[Configurable]
public class Vision : IDisposable
{
    [ConfigEntry] private static int TickRate { get; set; } = 100;

    private readonly Subscriber<DetectionFrame> _detectionSubscriber = Hub.RawDetection.Subscribe(Mode.All);

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private readonly Subscriber<CameraCalibration> _calibrationSubscriber = Hub.CameraCalibration.Subscribe(Mode.All);

    private readonly FilteredFrame _filteredFrame;
    
    private readonly Dictionary<uint, Camera> _cameras = new();

    private readonly RunnerSync _runner;

    public Vision()
    {
        _filteredFrame = new FilteredFrame();
        
        _runner = new RunnerSync(Tick, TickRate);
        _runner.Start();
    }

    private Camera GetOrCreateCamera(uint id)
    {
        if (_cameras.TryGetValue(id, out var camera)) return camera;

        camera = new Camera(id);
        _cameras.Add(id, camera);

        return camera;
    }

    private void ReceiveGeometry()
    {
        while (_calibrationSubscriber.Reader.TryRead(out var calibration))
        {
            var camera = GetOrCreateCamera(calibration.CameraId);
            camera.OnCalibration(calibration);
        }

        while (_fieldSizeSubscriber.Reader.TryRead(out var fieldSize))
        {
            foreach (var camera in _cameras.Values)
                camera.OnFieldSize(fieldSize);
        }
    }

    private void ReceiveDetections()
    {
        while (_detectionSubscriber.Reader.TryRead(out var frame))
        {
            var camera = GetOrCreateCamera(frame.CameraId);
            camera.OnFrame(frame, _filteredFrame);
        }
    }

    private void Tick()
    {
        ReceiveGeometry();
        ReceiveDetections();
    }

    public void Dispose()
    {
        _runner.Stop();
    }
}