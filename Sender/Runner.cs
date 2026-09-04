using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Sender;

[Configurable]
public sealed partial class Runner : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);
    [ConfigEntry] private static ThreadPriority RunnerPriority { get; set; } = ThreadPriority.AboveNormal;

    private readonly Subscriber<Common.Sender.Data.CommandsWrapper> _commandsSubscriber;
    
    private readonly RunnerSync _runner;

    private readonly List<ISender> _senders;

    public Runner()
    {
        _senders =
        [
            new Nrf(),
            new Simulator(),
            new ZmqRobotSender(),
        ];

        _commandsSubscriber = Hub.Commands.Subscribe(Mode.All);

        _runner = new RunnerSync(Tick, 0, ModuleName, RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        if (_commandsSubscriber.Reader.TryRead(out var commands))
        {
            foreach (var sender in _senders)
            {
                sender.Send(commands);
            }
        }
        else
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        _runner.Stop();

        foreach (var sender in _senders)
        {
            sender.Dispose();
        }
    }
}
