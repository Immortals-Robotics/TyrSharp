using System.Diagnostics;
using Tyr.Common.Runner;
using Xunit.Abstractions;

namespace Tyr.Tests.Common.Runner;

public class RunnerSyncTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Start_SetsIsRunningTrue()
    {
        var runner = new RunnerSync(() => Thread.Sleep(10));
        runner.Start();

        Assert.True(runner.IsRunning);
        runner.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunningFalse()
    {
        var runner = new RunnerSync(() => Thread.Sleep(10));
        runner.Start();
        runner.Stop();

        Assert.False(runner.IsRunning);
    }
    
    [Fact]
    public void Tick_IsCalledMultipleTimes()
    {
        var count = 0;
        var runner = new RunnerSync(() => Interlocked.Increment(ref count));
        runner.Start();

        Thread.Sleep(100); // let it tick a few times
        runner.Stop();

        Assert.True(count > 2, $"Expected more ticks, got {count}");
    }
    
    [Fact]
    public void TickRateHz_RespectsThrottling()
    {
        var count = 0;
        var runner = new RunnerSync(() => Interlocked.Increment(ref count), tickRateHz: 10);
        runner.Start();

        Thread.Sleep(250); // ~2-3 ticks expected at 10Hz
        runner.Stop();

        Assert.InRange(count, 1, 5); // be lenient due to thread scheduling
    }

    [Trait("Category", "Timing")]
    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(200)]
    public void TickRate_IsAccurate_AllowingMinorOutliers(int tickRateHz)
    {
        const float toleranceMs = 0.5f; // ±0.5ms allowed deviation
        const float maxOutlierRatio = 0.01f; // 1% of ticks can deviate
        const int testDurationMs = 1000;

        var expectedIntervalMs = 1000f / tickRateHz;
        var timestamps = new List<double>();
        var lockObj = new object();

        var stopwatch = Stopwatch.StartNew();
        var runner = new RunnerSync(() =>
        {
            lock (lockObj)
                timestamps.Add(stopwatch.Elapsed.TotalMilliseconds);
        }, tickRateHz);

        runner.Start();
        Thread.Sleep(testDurationMs);
        runner.Stop();
        stopwatch.Stop();

        lock (lockObj)
        {
            var expectedTicks = (int)(tickRateHz * testDurationMs / 1000f);
            Assert.True(timestamps.Count > expectedTicks - 5,
                $"Too few intervals captured at {tickRateHz} Hz. Expected {expectedTicks} ticks, got {timestamps.Count}");

            var intervals = timestamps.Zip(timestamps.Skip(1), (a, b) => b - a).Skip(1).ToList();

            var outliers = intervals.Count(i => System.Math.Abs(i - expectedIntervalMs) > toleranceMs);
            var outlierRatio = outliers / (float)intervals.Count;
            var meanInterval = intervals.Average();
            var meanError = System.Math.Abs(meanInterval - expectedIntervalMs);

            Assert.True(outlierRatio <= maxOutlierRatio,
                $"[{tickRateHz} Hz] Outlier ratio too high: {outlierRatio:P2} (allowed {maxOutlierRatio:P0})");

            Assert.True(meanError <= toleranceMs,
                $"[{tickRateHz} Hz] Mean tick interval error {meanError:F4}ms exceeds tolerance of {toleranceMs}ms");

            testOutputHelper.WriteLine(
                $"[{tickRateHz} Hz] Ticks: {intervals.Count} (expected: {expectedTicks}), Mean: {meanInterval:F4}ms, Outliers: {outliers}, Ratio: {outlierRatio:P2}");
        }
    }
}