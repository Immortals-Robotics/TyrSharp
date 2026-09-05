using Tyr.Common;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;

namespace Tyr.Vision;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry] private static partial int TickRateHz { get; set; } = 100;
    [ConfigEntry] private static partial ThreadPriority RunnerPriority { get; set; } = ThreadPriority.Highest;

    private readonly Subscriber<Detection.Frame> _detectionSubscriber = Hub.RawDetection.Subscribe(Mode.All);

    private readonly Subscriber<FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
    private readonly Subscriber<BallModels> _ballModelsSubscriber = Hub.BallModels.Subscribe(Mode.Latest);
    private readonly Subscriber<CameraCalibration> _calibrationSubscriber = Hub.CameraCalibration.Subscribe(Mode.All);

    private FieldSize? _latestFieldSize;
    private BallModels? _latestBallModels;

    private readonly RunnerSync _runner;

    private readonly Vision _vision = new();

    public Runner()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();

        _runner = new RunnerSync(Tick, TickRateHz, ModuleName, RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        if (_fieldSizeSubscriber.Reader.TryRead(out var fieldSize))
        {
            Vision.FieldSize = fieldSize;
            _latestFieldSize = fieldSize;
        }

        if (_ballModelsSubscriber.Reader.TryRead(out var ballModels))
        {
            _latestBallModels = ballModels;
        }

        if (BallParameters.UseVisionBallParameters)
        {
            if (_latestFieldSize.HasValue)
            {
                BallParameters.Apply(_latestFieldSize.Value);
            }

            if (_latestBallModels.HasValue)
            {
                BallParameters.Apply(_latestBallModels.Value);
            }
        }

        _vision.Process(_detectionSubscriber.All(), _calibrationSubscriber.All());

        Log.ZLogDebug($"fps: {_runner.Timer.Fps}");
        Plot.Plot("fps", _runner.Timer.Fps);

        return true;
    }

    public void Dispose()
    {
        _runner.Stop();
    }
}
