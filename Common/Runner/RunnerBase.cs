using Tyr.Common.Dataflow;
using Tyr.Common.Debug;
using Tyr.Common.Time;

namespace Tyr.Common.Runner;

public abstract class RunnerBase(int tickRateHz)
{
    public Time.Timer Timer { get; } = new();

    public int TickRateHz { get; } = tickRateHz;

    protected DeltaTime TickDuration => DeltaTime.FromSeconds(1.0 / TickRateHz);

    public Timestamp CurrentTickStartTimestamp { get; protected set; }

    static RunnerBase()
    {
        TimerResolution.Set(DeltaTime.FromMilliseconds(1));
    }

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

    protected void NewDebugFrame()
    {
        if (ModuleContext.Current.Value == null) return;

        var frame = new Frame
        {
            ModuleName = ModuleContext.Current.Value,
            StartTimestamp = CurrentTickStartTimestamp,
        };
        Hub.Frames.Publish(frame);

        // Send empty debug entries so that the frames can be sealed
        // even when the frames do not contain any actual entries
        Hub.Logs.Publish(Debug.Logging.Entry.Empty);
        Hub.Draws.Publish(Debug.Drawing.Command.Empty);
        Hub.Plots.Publish(Debug.Plotting.Command.Empty);
    }
}