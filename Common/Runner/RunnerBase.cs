using Tyr.Common.Dataflow;
using Tyr.Common.Debug;
using Tyr.Common.Time;

namespace Tyr.Common.Runner;

public abstract class RunnerBase(int tickRateHz)
{
    public abstract bool IsRunning { get; }

    public Time.Timer Timer { get; } = new();

    public int TickRateHz { get; } = tickRateHz;

    protected DeltaTime TickDuration => DeltaTime.FromSeconds(1.0 / TickRateHz);

    public Timestamp CurrentTickStartTimestamp { get; protected set; }

    static RunnerBase()
    {
        TimerResolution.Set(DeltaTime.FromMilliseconds(1));
    }

    public abstract void Start();
    public abstract void Stop();

    protected void SleepUntil(Timestamp nextTick)
    {
        var remaining = nextTick - Timer.Time;
        if (remaining > 2 * TimerResolution.Current)
        {
            var sleepTime = (remaining - TimerResolution.Current).ToTimeSpan();
            Thread.Sleep(sleepTime);
        }

        while (Timer.Time < nextTick) ;
    }

    public void NewDebugFrame()
    {
        if (ModuleContext.Current.Value == null) return;

        var frame = new Frame
        {
            ModuleName = ModuleContext.Current.Value,
            StartTimestamp = CurrentTickStartTimestamp,
        };
        Hub.Frames.Publish(frame);

        Draw.DrawEmpty();
        Plot.Plot(string.Empty, 0);
        Log.ZLogTrace($"");
    }
}