namespace Tyr.Common.Module;

public abstract class AsyncRunner
{
    private Task? _task;
    private CancellationTokenSource? _cts;

    protected Common.Time.Timer Timer { get; } = new();

    protected abstract Task Tick(CancellationToken token);

    protected virtual int TickRateHz => 0;

    private float TickDuration => 1f / TickRateHz;

    public void Start()
    {
        if (_task != null) return;

        _cts = new CancellationTokenSource();

        Timer.Start();

        _task = Task.Factory.StartNew(
            async () => await Loop(_cts.Token),
            _cts.Token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    public void Stop()
    {
        if (_cts == null) return;

        _cts.Cancel();
        _task?.Wait();

        Timer.Stop();
    }

    private async Task Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var tickStart = Timer.Time;

            Timer.Update();

            await Tick(token);

            if (TickRateHz <= 0) continue;

            var nextTick = tickStart + TickDuration;

            var remaining = nextTick - Timer.Time;
            if (remaining > 2f * TimerResolution.CurrentSeconds)
            {
                var sleepTime = (int)(1000f * (remaining - TimerResolution.CurrentSeconds));
                Logger.ZLogDebug($"Sleeping for {sleepTime}ms");
                Thread.Sleep(sleepTime);
            }

            while (Timer.Time < nextTick) ;
        }
    }
}