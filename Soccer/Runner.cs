using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

public sealed class Runner : IDisposable
{
    private readonly Subscriber<Referee.State> _refereeSubscriber;
    private readonly Subscriber<Vision.FilteredFrame> _visionSubscriber;

    private readonly RunnerSync _runner;

    public Runner()
    {
        _refereeSubscriber = Hub.Referee.Subscribe(Mode.All);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.All);

        _runner = new RunnerSync(Tick, 0, ModuleName);
        _runner.Start();
    }

    private bool Tick()
    {
        return false;
    }

    public void Dispose()
    {
        _refereeSubscriber.Dispose();
        _visionSubscriber.Dispose();

        _runner.Stop();
    }
}