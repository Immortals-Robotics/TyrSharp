using Tyr.Common.Time;

namespace Tyr.Common.Runner;

public abstract class RunnerBase(int tickRateHz)
{
    public abstract bool IsRunning { get; }

    public Time.Timer Timer { get; } = new();

    public int TickRateHz { get; } = tickRateHz;

    protected DeltaTime TickDuration => DeltaTime.FromSeconds(1.0 / TickRateHz);

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
}