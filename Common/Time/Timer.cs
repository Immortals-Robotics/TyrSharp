using System.Diagnostics;

namespace Tyr.Common.Time;

public class Timer
{
    private readonly Stopwatch _stopwatch = new();

    private long _timeOffsetTicks;
    private long _lastUpdateTicks;

    private const float SmoothnessAlpha = 0.1f;

    public float DeltaTime { get; private set; }
    public float DeltaTimeSmooth { get; private set; }

    public float Fps => 1f / DeltaTime;
    public float FpsSmooth => 1f / DeltaTimeSmooth;

    public bool Running => _stopwatch.IsRunning;

    public float Time => (float)((_stopwatch.ElapsedTicks + _timeOffsetTicks) / (double)Stopwatch.Frequency);

    public void SetTime(float value)
    {
        var targetTicks = (long)(value * Stopwatch.Frequency);
        var currentTicks = _stopwatch.ElapsedTicks;

        _timeOffsetTicks = targetTicks - currentTicks;
    }

    public void Reset()
    {
        _stopwatch.Restart();
        _lastUpdateTicks = _stopwatch.ElapsedTicks;
        _timeOffsetTicks = 0;
    }

    public void Restart()
    {
        Reset();
        Start();
    }

    public void Stop()
    {
        _stopwatch.Stop();
    }

    public void Start()
    {
        _stopwatch.Start();
        _lastUpdateTicks = _stopwatch.ElapsedTicks;
    }

    public void Update()
    {
        var nowTicks = _stopwatch.ElapsedTicks;
        var deltaTicks = nowTicks - _lastUpdateTicks;

        DeltaTime = (float)(deltaTicks / (double)Stopwatch.Frequency);
        DeltaTimeSmooth = SmoothnessAlpha * DeltaTime + (1f - SmoothnessAlpha) * DeltaTimeSmooth;

        _lastUpdateTicks = _stopwatch.ElapsedTicks;
    }
}