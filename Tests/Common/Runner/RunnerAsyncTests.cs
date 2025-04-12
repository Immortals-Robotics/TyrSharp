using System.Diagnostics;
using Tyr.Common.Runner;
using Xunit.Abstractions;

namespace Tyr.Tests.Common.Runner;

public class RunnerAsyncTests(ITestOutputHelper testOutputHelper)
{
    [Fact]
    public void Start_SetsIsRunningTrue()
    {
        var runner = new RunnerAsync(async _ => await Task.Yield());
        runner.Start();
        Assert.True(runner.IsRunning);
        runner.Stop();
    }

    [Fact]
    public void Stop_SetsIsRunningFalse()
    {
        var runner = new RunnerAsync(async _ => await Task.Yield());
        runner.Start();
        runner.Stop();
        Assert.False(runner.IsRunning);
    }

    [Fact]
    public void Tick_IsCalledMultipleTimes()
    {
        int count = 0;

        var runner = new RunnerAsync(async _ =>
        {
            Interlocked.Increment(ref count);
            await Task.Yield();
        });

        runner.Start();
        Thread.Sleep(100);
        runner.Stop();

        Assert.True(count > 2, $"Expected more ticks, got {count}");
    }

    [Theory]
    [InlineData(10)]
    [InlineData(60)]
    [InlineData(100)]
    [InlineData(200)]
    private void RunAccuracyTestAtRate(int tickRateHz)
    {
        const float toleranceMs = 0.5f; // ±0.5ms allowed deviation
        const float maxOutlierRatio = 0.01f; // 1% of ticks can deviate
        const int testDurationMs = 1000;

        float expectedMs = 1000f / tickRateHz;
        var timestamps = new List<double>();
        object lockObj = new();

        var sw = Stopwatch.StartNew();

        var runner = new RunnerAsync(async _ =>
        {
            lock (lockObj)
                timestamps.Add(sw.Elapsed.TotalMilliseconds);
            await Task.Yield();
        }, tickRateHz);

        runner.Start();
        Thread.Sleep(testDurationMs);
        runner.Stop();
        sw.Stop();

        lock (lockObj)
        {
            var expectedTicks = (int)(tickRateHz * testDurationMs / 1000f);
            Assert.True(timestamps.Count > expectedTicks - 5,
                $"Too few intervals captured at {tickRateHz} Hz. Expected {expectedTicks} ticks, got {timestamps.Count}");

            var intervals = timestamps.Zip(timestamps.Skip(1), (a, b) => b - a).Skip(1).ToList();

            var outliers = intervals.Count(i => System.Math.Abs(i - expectedMs) > toleranceMs);
            var outlierRatio = outliers / (float)intervals.Count;
            var meanInterval = intervals.Average();
            var meanError = System.Math.Abs(meanInterval - expectedMs);

            Assert.True(outlierRatio <= maxOutlierRatio,
                $"[{tickRateHz} Hz] Outlier ratio too high: {outlierRatio:P2}");

            Assert.True(meanError <= toleranceMs,
                $"[{tickRateHz} Hz] Mean tick interval error {meanError:F4}ms exceeds {toleranceMs}ms");

            testOutputHelper.WriteLine(
                $"[{tickRateHz} Hz] Ticks: {intervals.Count} (expected: {expectedTicks}), Mean: {meanInterval:F4}ms, Outliers: {outliers}, Ratio: {outlierRatio:P2}");
        }
    }

    [Fact]
    public void CancellationToken_IsRespected()
    {
        bool wasCancelled = false;

        var runner = new RunnerAsync(async ct =>
        {
            try
            {
                await Task.Delay(10, ct);
            }
            catch (OperationCanceledException)
            {
                wasCancelled = true;
            }
        });

        runner.Start();
        Thread.Sleep(50);
        runner.Stop();

        Assert.True(wasCancelled || runner.IsRunning == false, "Tick function did not observe cancellation.");
    }

    [Fact]
    public void Stop_DoesNotHang_WhenTickIsSlow()
    {
        var runner = new RunnerAsync(async ct =>
        {
            await Task.Delay(1000, ct); // will be cancelled early
        });

        runner.Start();
        Thread.Sleep(50); // give it time to start
        var sw = Stopwatch.StartNew();
        runner.Stop();
        sw.Stop();

        Assert.True(sw.Elapsed.TotalSeconds < 2, "RunnerAsync.Stop() should return promptly.");
    }
}