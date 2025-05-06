using Tyr.Common;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Vision.Trajectory;

namespace Tyr.Vision;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry] private static int TickRateHz { get; set; } = 100;

    private readonly Subscriber<Detection.Frame> _detectionSubscriber = Hub.RawDetection.Subscribe(Mode.All);

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private readonly Subscriber<CameraCalibration> _calibrationSubscriber = Hub.CameraCalibration.Subscribe(Mode.All);

    private readonly RunnerSync _runner;

    private readonly Vision _vision = new();

    public Runner()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();

        _runner = new RunnerSync(Tick, TickRateHz, ModuleName);
        _runner.Start();
    }

    private void Tick()
    {
        if (_fieldSizeSubscriber.TryGetLatest(out var fieldSize))
        {
            Vision.FieldSize = fieldSize;
        }

        _vision.Process(_detectionSubscriber.All(), _calibrationSubscriber.All());
    }

    public void Dispose()
    {
        _runner.Stop();
    }
}