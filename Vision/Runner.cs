using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;

namespace Tyr.Vision;

[Configurable]
public sealed class Runner : IDisposable
{
    [ConfigEntry] private static int TickRateHz { get; set; } = 100;

    private readonly Subscriber<DetectionFrame> _detectionSubscriber = Hub.RawDetection.Subscribe(Mode.All);

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private readonly Subscriber<CameraCalibration> _calibrationSubscriber = Hub.CameraCalibration.Subscribe(Mode.All);

    private readonly RunnerSync _runner;

    private readonly Vision _vision = new();

    public Runner()
    {
        _runner = new RunnerSync(Tick, TickRateHz);
        _runner.Start();
    }

    private void Tick()
    {
        var frame = new Common.Debug.Frame
        {
            ModuleName = ModuleName,
            StartTimestamp = _runner.CurrentTickStartTimestamp,
        };
        Hub.Frames.Publish(frame);

        FieldSize? fieldSize = _fieldSizeSubscriber.TryGetLatest(out var f) ? f : null;

        _vision.Process(
            _detectionSubscriber.All(),
            _calibrationSubscriber.All(),
            fieldSize);
    }

    public void Dispose()
    {
        _runner.Stop();
    }
}