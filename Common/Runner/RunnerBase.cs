namespace Tyr.Common.Runner;

public abstract class RunnerBase(int tickRateHz)
{
    public abstract bool IsRunning { get; }

    public Common.Time.Timer Timer { get; } = new();

    public int TickRateHz { get; } = tickRateHz;

    protected float TickDuration => 1f / TickRateHz;

    static RunnerBase()
    {
        TimerResolution.Set(1);
    }

    public abstract void Start();
    public abstract void Stop();

    protected void SleepUntil(float nextTick)
    {
        var remaining = nextTick - Timer.Time;
        if (remaining > 2f * TimerResolution.CurrentSeconds)
        {
            var sleepTime = (int)(1000f * (remaining - TimerResolution.CurrentSeconds));
            Logger.ZLogTrace($"Sleeping for {sleepTime}ms");
            Thread.Sleep(sleepTime);
        }

        while (Timer.Time < nextTick) ;
    }
}