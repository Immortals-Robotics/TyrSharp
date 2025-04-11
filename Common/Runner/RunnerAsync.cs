namespace Tyr.Common.Runner;

public class RunnerAsync(Func<CancellationToken, Task> tick, int tickRateHz = 0)
    : RunnerBase(tickRateHz)
{
    private Task? _task;
    private CancellationTokenSource? _cts;

    public override bool IsRunning => _task != null;

    public override void Start()
    {
        if (IsRunning) return;

        Timer.Start();

        _cts = new CancellationTokenSource();

        Timer.Start();

        _task = Task.Factory.StartNew(
            async () => await Loop(_cts.Token),
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public override void Stop()
    {
        if (!IsRunning) return;

        _cts!.Cancel();
        _task!.Wait();

        Timer.Stop();
    }

    private async Task Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var tickStart = Timer.Time;

            Timer.Update();

            await tick!(token);

            if (TickRateHz <= 0) continue;

            var nextTick = tickStart + TickDuration;
            SleepUntil(nextTick);
        }
    }
}