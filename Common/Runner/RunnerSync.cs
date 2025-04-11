namespace Tyr.Common.Runner;

public class RunnerSync(Action tick, int tickRateHz = 0) : RunnerBase(tickRateHz)
{
    private Thread? _thread;
    private volatile bool _running;

    public override bool IsRunning => _running;

    public override void Start()
    {
        if (IsRunning) return;

        Timer.Start();

        _running = true;

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = $"{tick.Method.DeclaringType?.FullName ?? "Unknown"}:{tick.Method.Name}"
        };
        _thread.Start();
    }

    public override void Stop()
    {
        if (!IsRunning) return;

        _running = false;
        _thread?.Join();

        Timer.Stop();
    }

    private void Loop()
    {
        while (_running)
        {
            var tickStart = Timer.Time;

            Timer.Update();
            tick();

            if (TickRateHz <= 0) continue;

            var nextTick = tickStart + TickDuration;
            SleepUntil(nextTick);
        }
    }
}