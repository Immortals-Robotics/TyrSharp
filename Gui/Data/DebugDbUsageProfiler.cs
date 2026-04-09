using System.Collections;
using System.Collections.Concurrent;
using System.Diagnostics;
using Tyr.Common.Config;

namespace Tyr.Gui.Data;

[Configurable]
internal static partial class DebugDbUsageProfiler
{
    [ConfigEntry(StorageType.User)] private static bool Enabled { get; set; } = true;

    private static readonly Lock Sync = new();
    private static readonly Dictionary<string, Stat> CurrentFrame = new(StringComparer.Ordinal);
    private static int _frameIndex;

    public static void BeginFrame()
    {
        if (!Enabled)
            return;

        lock (Sync)
            CurrentFrame.Clear();
    }

    public static void EndFrame()
    {
        if (!Enabled)
            return;

        KeyValuePair<string, Stat>[] snapshot;
        int frameIndex;

        lock (Sync)
        {
            if (CurrentFrame.Count == 0)
                return;

            frameIndex = ++_frameIndex;
            snapshot = CurrentFrame
                .OrderByDescending(pair => pair.Value.TotalTicks)
                .ToArray();
            CurrentFrame.Clear();
        }

        var totalTicks = snapshot.Sum(pair => pair.Value.TotalTicks);
        Log.ZLogDebug($"DebugDb frame {frameIndex}: db-total={TicksToMilliseconds(totalTicks):F3} ms");

        foreach (var (name, stat) in snapshot)
            Log.ZLogDebug(
                $"DebugDb frame {frameIndex}: {name} count={stat.Count} avg={TicksToMilliseconds(stat.TotalTicks) / stat.Count:F3} ms max={TicksToMilliseconds(stat.MaxTicks):F3} ms total={TicksToMilliseconds(stat.TotalTicks):F3} ms");
    }

    public static T Measure<T>(string name, Func<T> action)
    {
        if (!Enabled)
            return action();

        var start = Stopwatch.GetTimestamp();
        try
        {
            return action();
        }
        finally
        {
            Record(name, Stopwatch.GetTimestamp() - start);
        }
    }

    public static IEnumerable<T> MeasureEnumerable<T>(string name, IEnumerable<T> source)
    {
        if (!Enabled)
            return source;

        return new MeasuredEnumerable<T>(name, source);
    }

    private static void Record(string name, long elapsedTicks)
    {
        lock (Sync)
        {
            if (!CurrentFrame.TryGetValue(name, out var stat))
                stat = default;

            stat.Count++;
            stat.TotalTicks += elapsedTicks;
            stat.MaxTicks = Math.Max(stat.MaxTicks, elapsedTicks);
            CurrentFrame[name] = stat;
        }
    }

    private static double TicksToMilliseconds(long ticks) => ticks * 1000d / Stopwatch.Frequency;

    private struct Stat
    {
        public int Count;
        public long TotalTicks;
        public long MaxTicks;
    }

    private sealed class MeasuredEnumerable<T>(string name, IEnumerable<T> source) : IEnumerable<T>
    {
        public IEnumerator<T> GetEnumerator() => new MeasuredEnumerator<T>(name, source.GetEnumerator());

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class MeasuredEnumerator<T>(string name, IEnumerator<T> inner) : IEnumerator<T>
    {
        private long _elapsedTicks;
        private bool _recorded;

        public T Current => inner.Current;

        object IEnumerator.Current => Current!;

        public bool MoveNext()
        {
            var start = Stopwatch.GetTimestamp();
            try
            {
                return inner.MoveNext();
            }
            finally
            {
                _elapsedTicks += Stopwatch.GetTimestamp() - start;
            }
        }

        public void Reset() => inner.Reset();

        public void Dispose()
        {
            try
            {
                inner.Dispose();
            }
            finally
            {
                if (!_recorded)
                {
                    _recorded = true;
                    Record(name, _elapsedTicks);
                }
            }
        }
    }
}
