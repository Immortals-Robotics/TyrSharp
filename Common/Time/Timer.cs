﻿using System.Diagnostics;

namespace Tyr.Common.Time;

public class Timer
{
    private readonly Stopwatch _stopwatch = new();

    private long _timeOffsetTicks;
    private long _lastUpdateTicks;

    private const double SmoothnessAlpha = 0.1f;

    public DeltaTime DeltaTime { get; private set; }
    public DeltaTime DeltaTimeSmooth { get; private set; }

    public float Fps => (float)(1 / DeltaTime.Seconds);
    public float FpsSmooth => (float)(1 / DeltaTimeSmooth.Seconds);

    public bool Running => _stopwatch.IsRunning;

    public Timestamp Time =>
        Timestamp.FromSeconds((_stopwatch.ElapsedTicks + _timeOffsetTicks) / (double)Stopwatch.Frequency);

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

        DeltaTime = DeltaTime.FromSeconds(deltaTicks / (double)Stopwatch.Frequency);
        DeltaTimeSmooth = SmoothnessAlpha * DeltaTime + (1f - SmoothnessAlpha) * DeltaTimeSmooth;

        _lastUpdateTicks = _stopwatch.ElapsedTicks;
    }
}