using Tyr.Common.Config;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Vision.Data;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;

namespace Tyr.Referee;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry] private static int TickRateHz { get; set; } = 100;

    private readonly Subscriber<Gc.Referee> _gcSubscriber;
    private readonly Subscriber<FilteredFrame> _visionSubscriber;

    private readonly RunnerSync _runner;

    private FilteredFrame? _vision;
    private Gc.Referee? _gc;

    private readonly Referee _referee = new();

    public Runner()
    {
        _gcSubscriber = Hub.RawReferee.Subscribe(Mode.All);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.Latest);

        _runner = new RunnerSync(Tick, TickRateHz, ModuleName);
        _runner.Start();
    }

    private bool Tick()
    {
        var shouldProcess = false;

        if (_visionSubscriber.Reader.TryRead(out var vision))
        {
            _vision = vision;
            shouldProcess = true;
        }

        if (_gcSubscriber.Reader.TryRead(out var gc))
        {
            _gc = gc;
            shouldProcess = true;
        }

        if (!shouldProcess || !_referee.Process(_vision, _gc)) return false;

        Hub.Referee.Publish(_referee.State);
        return true;
    }

    public void Dispose()
    {
        _gcSubscriber.Dispose();
        _visionSubscriber.Dispose();

        _runner.Stop();
    }
}