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

        try
        {
            if (!_task!.Wait(5000)) // waits up to 5 seconds
            {
                throw new TimeoutException("RunnerAsync.Stop() timed out waiting for task to complete.");
            }
        }
        catch (AggregateException ex)
        {
            // Ignore cancellation-related exceptions
            if (!ex.InnerExceptions.Any(e => e is TaskCanceledException or OperationCanceledException))
                throw;
        }

        Timer.Stop();
        _task = null;
    }

    private async Task Loop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var tickStart = Timer.Time;
            CurrentTickStartTimestamp = Timestamp.Now;

            Timer.Update();

            await tick(token);

            if (TickRateHz <= 0) continue;

            var nextTick = tickStart + TickDuration;
            SleepUntil(nextTick);
        }
    }
}