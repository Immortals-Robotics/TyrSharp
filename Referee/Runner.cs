using Tracker = Tyr.Common.Data.Ssl.Vision.Tracker;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;

namespace Tyr.Referee;

public sealed class Runner : IDisposable
{
    private readonly Subscriber<Gc.Referee> _gcSubscriber;
    private readonly Subscriber<Tracker.Frame> _visionSubscriber;

    private readonly RunnerAsync _runner;

    private Tracker.Frame? _vision;
    private Gc.Referee? _gc;

    private readonly Referee _referee = new();

    public Runner()
    {
        _gcSubscriber = Hub.RawReferee.Subscribe(Mode.All);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.Latest);

        _runner = new RunnerAsync(Tick, 0, ModuleName);
        _runner.Start();
    }

    private async Task Tick(CancellationToken token)
    {
        await Task.WhenAny(ReceiveGc(token), ReceiveVision(token));

        var frame = new Common.Debug.Frame
        {
            ModuleName = ModuleName,
            StartTimestamp = _runner.CurrentTickStartTimestamp,
        };
        Hub.Frames.Publish(frame);

        if (_referee.Process(_vision, _gc))
        {
            Hub.Referee.Publish(_referee.State);
        }
    }

    private async Task ReceiveGc(CancellationToken token)
    {
        try
        {
            _gc = await _gcSubscriber.Reader.ReadAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task ReceiveVision(CancellationToken token)
    {
        try
        {
            _vision = await _visionSubscriber.Reader.ReadAsync(token);
        }
        catch (OperationCanceledException)
        {
        }
    }

    public void Dispose()
    {
        _gcSubscriber.Dispose();
        _visionSubscriber.Dispose();

        _runner.Stop();
    }
}