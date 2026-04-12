using System.Threading;
using Xunit;
using Tyr.Common.Time;
using Timer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Common.Time;

public class TimerTests
{
    [Fact]
    public void Start_SetsRunningToTrue()
    {
        var timer = new Timer();
        Assert.False(timer.Running);
        timer.Start();
        Assert.True(timer.Running);
    }

    [Fact]
    public void Stop_SetsRunningToFalse()
    {
        var timer = new Timer();
        timer.Start();
        Assert.True(timer.Running);
        timer.Stop();
        Assert.False(timer.Running);
    }

    [Fact]
    public void Reset_ResetsTimeAndStarts()
    {
        var timer = new Timer();
        timer.Start();
        Thread.Sleep(20);
        Assert.True(timer.Time.Milliseconds > 0);

        timer.Reset();

        Assert.True(timer.Running);
        Assert.True(timer.Time.Milliseconds < 20);
    }

    [Fact]
    public void Restart_ResetsAndStarts()
    {
        var timer = new Timer();
        timer.Start();
        Thread.Sleep(20);
        var timeBefore = timer.Time;
        Assert.True(timeBefore.Milliseconds >= 15);

        timer.Restart();

        Assert.True(timer.Running);
        Assert.True(timer.Time.Nanoseconds < timeBefore.Nanoseconds);
        Assert.True(timer.Time.Milliseconds < 20);
    }

    [Fact]
    public void SetTime_OffsetsTime()
    {
        var timer = new Timer();
        timer.SetTime(100f); // 100 seconds

        Assert.Equal(100.0, timer.Time.Seconds, 1);
    }

    [Fact]
    public void Update_CalculatesDeltaTime()
    {
        var timer = new Timer();
        timer.Start();

        timer.Update(); // First update to set lastUpdateTicks
        Thread.Sleep(25);
        timer.Update();

        Assert.True(timer.DeltaTime.Milliseconds >= 20);
        Assert.True(timer.DeltaTimeSmooth.Milliseconds > 0);
    }

    [Fact]
    public void Fps_CalculatesCorrectly()
    {
        var timer = new Timer();
        timer.Start();
        timer.Update();
        Thread.Sleep(20); // ~50fps
        timer.Update();

        Assert.InRange(timer.Fps, 20, 100);
    }
}
