using Tyr.Common.Debug;

namespace Tyr.Common.Runner;

public class RunnerSync(Func<bool> tick, int tickRateHz = 0, string? callingModule = null) : RunnerBase(tickRateHz)
{
    private Func<bool>? _init = null;
    private Thread? _thread;
    private volatile bool _running;

    public bool IsRunning => _running;

    public void SetInit(Func<bool> init)
    {
        _init = init;
    }
    
    public void Start()
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

    public void StartOnCurrentThread()
    {
        if (IsRunning) return;

        Timer.Start();

        _running = true;

        Loop();
    }

    public void Stop()
    {
        if (!IsRunning) return;

        _running = false;
        _thread?.Join();

        Timer.Stop();
    }

    private void Loop()
    {
        ModuleContext.Current.Value = callingModule;

        if (_init != null)
        {
            CurrentTickStartTimestamp = Timestamp.Now;
            Timer.Update();
            
            if (_init()) NewDebugFrame();
        }

        var ticked = false;
        while (_running)
        {
            var tickStart = Timer.Time;
            CurrentTickStartTimestamp = Timestamp.Now;

            if (ticked) Timer.Update();
            
            ticked = tick();
            if (ticked) NewDebugFrame();
            if (TickRateHz <= 0) continue;

            var nextTick = tickStart + TickDuration;
            SleepUntil(nextTick);
        }
    }
}