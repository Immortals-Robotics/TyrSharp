using System.Diagnostics;
using System.Reflection;
using Tyr.Common.Dataflow;
using ZLogger;

namespace Tyr.Common.Debug.Db;

/// <summary>Ingest counters for one entry type. Snapshot; cheap to build, never taken per pump.</summary>
public readonly record struct IngestTypeStats(Type EntryType, long Drained, int Backlog, bool BacklogKnown);

/// <summary>
/// Moves published debug entries and frames from <see cref="DebugBus"/> into a <see cref="DebugDb"/>.
/// One subscriber per registered entry type; types registered later (an assembly loaded after
/// startup) are picked up through <see cref="DebugTypeRegistry.Registered"/>. The entry type is
/// generic all the way down, so nothing is boxed or copied through a union.
/// </summary>
public sealed class DebugDbIngest : IDisposable
{
    /// <summary>Backlog above which a type is considered to be falling behind.</summary>
    private const int BacklogWarnThreshold = 4096;

    /// <summary>Consecutive pumps a type must stay above the threshold before it is reported.</summary>
    private const int BacklogWarnPumps = 10;

    /// <summary>Minimum gap between backlog warnings, whichever type triggers them.</summary>
    private static readonly long BacklogWarnIntervalTicks = Stopwatch.Frequency * 10;

    /// <summary>
    /// Hard cap on frames held back waiting for entry channels to catch up. Only a backstop:
    /// the timestamp rule below normally releases frames every pump.
    /// </summary>
    private const int MaxPendingFrames = 4096;

    private interface IDrain : IDisposable
    {
        Type EntryType { get; }

        /// <summary>Total entries appended to the database by this drain.</summary>
        long Drained { get; }

        /// <summary>Entries still queued, or -1 when the channel cannot report a count.</summary>
        int Backlog { get; }

        /// <summary>
        /// Timestamp of the last entry this drain appended, in nanoseconds. Entries still
        /// queued behind it were published later, so frames up to this point are safe to make
        /// visible even while the drain is still backlogged.
        /// </summary>
        long LastDrainedTimestampNs { get; }

        /// <summary>Consecutive pumps this drain has been over the backlog threshold.</summary>
        int BacklogStreak { get; set; }

        int Pump(DebugDb db, int budget);
    }

    private sealed class Drain<T> : IDrain where T : struct, IEntry
    {
        private readonly Subscriber<T> _subscriber = DebugBus.Subscribe<T>(Mode.All);

        public Type EntryType => typeof(T);
        public long Drained { get; private set; }
        public long LastDrainedTimestampNs { get; private set; } = long.MinValue;
        public int BacklogStreak { get; set; }

        public int Backlog
        {
            get
            {
                var reader = _subscriber.Reader;
                return reader.CanCount ? reader.Count : -1;
            }
        }

        public int Pump(DebugDb db, int budget)
        {
            var reader = _subscriber.Reader;
            var count = 0;
            while (count < budget && reader.TryRead(out var entry))
            {
                db.Append(entry);
                LastDrainedTimestampNs = entry.Timestamp.Nanoseconds;
                count++;
            }

            Drained += count;
            return count;
        }

        public void Dispose() => _subscriber.Dispose();
    }

    private static readonly MethodInfo CreateDrainMethod =
        typeof(DebugDbIngest).GetMethod(nameof(CreateDrain), BindingFlags.Static | BindingFlags.NonPublic)!;

    private readonly DebugDb _db;
    private readonly Subscriber<Frame> _frames = DebugBus.SubscribeFrames(Mode.All);
    private readonly Lock _drainsLock = new();
    private readonly HashSet<Type> _drainedTypes = [];
    private IDrain[] _drains = [];
    private readonly List<Frame> _pendingFrames = [];
    private long _framesIngested;
    private long _framesForcedOut;
    private long _lastBacklogWarningTicks = long.MinValue;
    private bool _disposed;

    /// <summary>Raised on the pumping thread for every frame ingested.</summary>
    public event Action<Frame>? FrameIngested;

    /// <summary>Frames appended to the database so far.</summary>
    public long FramesIngested => _framesIngested;

    /// <summary>Frames released early because <see cref="MaxPendingFrames"/> was hit.</summary>
    public long FramesForcedOut => _framesForcedOut;

    /// <summary>Frames currently held back waiting for entry channels to catch up.</summary>
    public int PendingFrames => _pendingFrames.Count;

    public DebugDbIngest(DebugDb db)
    {
        _db = db;
        DebugTypeRegistry.Registered += OnTypeRegistered;
    }

    /// <summary>Fills <paramref name="destination"/> with one entry per registered type.</summary>
    public void GetStats(List<IngestTypeStats> destination)
    {
        ArgumentNullException.ThrowIfNull(destination);

        destination.Clear();
        foreach (var drain in Volatile.Read(ref _drains))
        {
            var backlog = drain.Backlog;
            destination.Add(new IngestTypeStats(drain.EntryType, drain.Drained, backlog < 0 ? 0 : backlog, backlog >= 0));
        }
    }

    private void OnTypeRegistered(Type type)
    {
        // Claim the type and open its buckets under the lock (so neither can happen after
        // Dispose), but subscribe outside it: subscribing runs the channel's static
        // constructor, which itself registers the type and re-enters here, possibly from
        // another thread, so it must never have to wait on our lock.
        lock (_drainsLock)
        {
            if (_disposed || !_drainedTypes.Add(type))
                return;

            try
            {
                _db.RegisterType(type);
            }
            catch
            {
                _drainedTypes.Remove(type);
                throw;
            }
        }

        IDrain drain;
        try
        {
            drain = (IDrain)CreateDrainMethod.MakeGenericMethod(type).Invoke(null, null)!;
        }
        catch
        {
            lock (_drainsLock)
                _drainedTypes.Remove(type);
            throw;
        }

        lock (_drainsLock)
        {
            if (_disposed)
            {
                drain.Dispose();
                return;
            }

            var drains = _drains;
            var grown = new IDrain[drains.Length + 1];
            Array.Copy(drains, grown, drains.Length);
            grown[^1] = drain;
            Volatile.Write(ref _drains, grown);
        }
    }

    private static IDrain CreateDrain<T>() where T : struct, IEntry => new Drain<T>();

    /// <summary>
    /// Ingests up to <paramref name="budget"/> items per source. Returns true when anything
    /// was ingested, so the caller knows whether to sleep.
    ///
    /// A frame closes the previous frame's time window for readers, so a frame must never
    /// become visible before the entries published ahead of it. Entries and frames travel on
    /// separate channels, so frames are taken first, then every entry channel is drained, and
    /// a taken frame is appended only once no entry channel can still be holding an older
    /// entry: a drain that ran empty holds nothing back at all, and a drain that is still
    /// backlogged holds back only frames newer than the last entry it appended (its queue is
    /// FIFO, so everything still in it was published after that entry). Frame visibility
    /// therefore keeps advancing under sustained backlog instead of stalling completely.
    /// </summary>
    public bool Pump(int budget = 1000)
    {
        var frames = _frames.Reader;
        var taken = 0;
        while (taken < budget && frames.TryRead(out var frame))
        {
            _pendingFrames.Add(frame);
            taken++;
        }

        var any = taken > 0;
        var safeUpToNs = long.MaxValue;
        var backlogged = false;

        foreach (var drain in Volatile.Read(ref _drains))
        {
            var count = drain.Pump(_db, budget);
            any |= count > 0;

            if (count >= budget)
            {
                // Still behind: only frames up to what it just drained are safe.
                var last = drain.LastDrainedTimestampNs;
                if (last < safeUpToNs)
                    safeUpToNs = last;
            }

            backlogged |= TrackBacklog(drain);
        }

        if (backlogged)
            WarnAboutBacklog();

        ReleaseFrames(safeUpToNs);
        return any;
    }

    /// <summary>Updates a drain's backlog streak. Returns true once it has been over the line long enough.</summary>
    private static bool TrackBacklog(IDrain drain)
    {
        var backlog = drain.Backlog;
        if (backlog < BacklogWarnThreshold)
        {
            drain.BacklogStreak = 0;
            return false;
        }

        drain.BacklogStreak++;
        return drain.BacklogStreak >= BacklogWarnPumps;
    }

    private void WarnAboutBacklog()
    {
        var now = Stopwatch.GetTimestamp();
        if (now - _lastBacklogWarningTicks < BacklogWarnIntervalTicks)
            return;

        _lastBacklogWarningTicks = now;

        foreach (var drain in Volatile.Read(ref _drains))
        {
            if (drain.BacklogStreak < BacklogWarnPumps)
                continue;

            Log.ZLogWarning(
                $"Debug ingest is falling behind on {drain.EntryType.Name}: {drain.Backlog} entries queued " +
                $"for {drain.BacklogStreak} consecutive pumps ({drain.Drained} ingested so far). " +
                $"Reduce debug drawing or raise the ingest budget.");
        }
    }

    /// <summary>
    /// Appends every pending frame at or before <paramref name="safeUpToNs"/>, keeping the rest
    /// in order. If the backlog of held frames hits the cap, the oldest are released anyway.
    /// </summary>
    private void ReleaseFrames(long safeUpToNs)
    {
        if (_pendingFrames.Count == 0)
            return;

        var forced = _pendingFrames.Count - MaxPendingFrames;
        if (forced > 0)
        {
            _framesForcedOut += forced;
            Log.ZLogWarning(
                $"Debug ingest held {_pendingFrames.Count} frames waiting for entry channels; " +
                $"releasing the oldest {forced} early. Frame windows may miss late entries.");
        }

        var kept = 0;
        for (var i = 0; i < _pendingFrames.Count; i++)
        {
            var frame = _pendingFrames[i];
            if (i >= forced && frame.StartTimestamp.Nanoseconds > safeUpToNs)
            {
                _pendingFrames[kept++] = frame;
                continue;
            }

            _db.AppendFrame(frame);
            _framesIngested++;
            FrameIngested?.Invoke(frame);
        }

        _pendingFrames.RemoveRange(kept, _pendingFrames.Count - kept);
    }

    public void Dispose()
    {
        DebugTypeRegistry.Registered -= OnTypeRegistered;

        IDrain[] drains;
        lock (_drainsLock)
        {
            _disposed = true;
            drains = _drains;
            _drains = [];
        }

        foreach (var drain in drains)
            drain.Dispose();

        _frames.Dispose();
    }
}
