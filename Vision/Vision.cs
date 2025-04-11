using System.Threading.Channels;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;

namespace Tyr.Vision;

public class Vision : IDisposable
{
    private readonly ChannelReader<Frame> _reader = Hub.RawDetection.Subscribe();

    private Dictionary<uint, Camera> _cameras = new();

    private readonly RunnerSync _runner;

    private Common.Time.Timer Timer => _runner.Timer;

    public Vision()
    {
        _runner = new RunnerSync(Tick, 100);
        _runner.Start();
    }

    private void ReceiveDetections()
    {
        while (_reader.TryRead(out var frame))
        {
            if (!_cameras.TryGetValue(frame.CameraId, out var camera))
            {
                camera = new Camera(frame.CameraId);
                _cameras.Add(frame.CameraId, camera);
            }

            camera.OnFrame(frame);
        }

        Logger.ZLogDebug($"fps: requested: {_runner.TickRateHz}, actual: {1f / Timer.DeltaTime:F2}");
    }

    private void Tick()
    {
        ReceiveDetections();
    }

    public void Dispose()
    {
        _runner.Stop();
    }
}