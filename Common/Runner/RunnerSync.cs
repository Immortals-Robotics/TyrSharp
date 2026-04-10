using Tyr.Common.Debug;

namespace Tyr.Common.Runner;

public class RunnerSync(
    Func<bool> tick,
    int tickRateHz = 0,
    string? callingModule = null,
    ThreadPriority priority = ThreadPriority.Normal) : RunnerBase(tickRateHz)
{
    private Func<bool>? _init = null;
    private Thread? _thread;
    private Thread? _executingThread;
    private volatile bool _running;
    private ThreadPriority _priority = priority;

    public bool IsRunning => _running;
    public ThreadPriority Priority => _priority;

    public void SetInit(Func<bool> init)
    {
        _init = init;
    }

    public void SetPriority(ThreadPriority priority)
    {
        _priority = priority;
        TrySetPriority(_executingThread ?? _thread, priority);
    }
    
    public void Start()
    {
        if (IsRunning) return;

        Timer.Start();

        _running = true;

        _thread = new Thread(Loop)
        {
            IsBackground = true,
            Name = $"{tick.Method.DeclaringType?.FullName ?? "Unknown"}:{tick.Method.Name}",
            Priority = _priority
        };
        _thread.Start();
    }

    public void StartOnCurrentThread()
    {
        if (IsRunning) return;

        Timer.Start();

        _running = true;

        var thread = Thread.CurrentThread;
        var previousPriority = thread.Priority;
        _executingThread = thread;
        TrySetPriority(thread, _priority);

        try
        {
            Loop();
        }
        finally
        {
            TrySetPriority(thread, previousPriority);
            if (ReferenceEquals(_executingThread, thread))
            {
                _executingThread = null;
            }
        }
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
        _executingThread = Thread.CurrentThread;
        ModuleContext.Current.Value = callingModule;

        try
        {
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
        finally
        {
            if (ReferenceEquals(_executingThread, Thread.CurrentThread))
            {
                _executingThread = null;
            }
        }
    }

    private static void TrySetPriority(Thread? thread, ThreadPriority priority)
    {
        if (thread == null) return;

        try
        {
            thread.Priority = priority;
        }
        catch (ThreadStateException)
        {
            // The runner can be stopping while configs are updating; in that case we just keep going.
        }
    }
}
