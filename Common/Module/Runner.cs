using System.Diagnostics;

namespace Tyr.Common.Module;

public abstract class Runner
{
    private Thread? _thread;
    private volatile bool _running;

    protected Common.Time.Timer Timer { get; } = new();

    protected abstract void Tick();

    protected abstract string Name { get; }

    protected virtual int TickRateHz => 0;

    private float TickDuration => 1f / TickRateHz;

    public void Start()
    {
        if (_running) return;
        _running = true;

        Timer.Start();

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = Name
        };
        _thread.Start();
    }

    public void Stop()
    {
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

            Tick();

            if (TickRateHz <= 0) continue;

            var nextTick = tickStart + TickDuration;
            while (Timer.Time < nextTick) ;
        }
    }
}