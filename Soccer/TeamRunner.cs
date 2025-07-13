using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

[Configurable]
public sealed partial class TeamRunner : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);

    private readonly Subscriber<Referee.State> _refereeSubscriber;
    private readonly Subscriber<Vision.FilteredFrame> _visionSubscriber;

    private readonly RunnerSync _runner;

    private readonly TeamColor _color;

    private Vision.FilteredFrame? _vision;
    private Referee.State? _referee;

    public TeamRunner(TeamColor color)
    {
        _color = color;

        _refereeSubscriber = Hub.Referee.Subscribe(Mode.Latest);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.All);

        _runner = new RunnerSync(Tick, 0, $"{ModuleName}{_color}");
    }

    public void Start() => _runner.Start();
    public void Stop() => _runner.Stop();

    private bool Tick()
    {
        if (!_visionSubscriber.Reader.TryRead(out var vision))
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        _vision = vision;

        if (_refereeSubscriber.Reader.TryRead(out var referee))
            _referee = referee;

        Log.ZLogDebug($"Team {_color} tick");
        return true;
    }

    public void Dispose()
    {
        _refereeSubscriber.Dispose();
        _visionSubscriber.Dispose();

        _runner.Stop();
    }
}