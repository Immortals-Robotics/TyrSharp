using Tyr.Common.Config;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Vision.Data;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Referee;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);

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

        _runner = new RunnerSync(Tick, 0, ModuleName);
        _runner.Start();
    }

    private bool Tick()
    {
        var newData = false;

        if (_visionSubscriber.Reader.TryRead(out var vision))
        {
            _vision = vision;
            newData = true;
        }

        if (_gcSubscriber.Reader.TryRead(out var gc))
        {
            _gc = gc;
            newData = true;
        }

        if (!newData)
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        if (!_referee.Process(_vision, _gc))
        {
            return false;
        }

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