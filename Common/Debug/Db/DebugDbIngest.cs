using System.Reflection;
using Tyr.Common.Dataflow;

namespace Tyr.Common.Debug.Db;

/// <summary>
/// Moves published debug entries and frames from <see cref="DebugBus"/> into a <see cref="DebugDb"/>.
/// One subscriber per registered entry type; types registered later (an assembly loaded after
/// startup) are picked up through <see cref="DebugTypeRegistry.Registered"/>. The entry type is
/// generic all the way down, so nothing is boxed or copied through a union.
/// </summary>
public sealed class DebugDbIngest : IDisposable
{
    private interface IDrain : IDisposable
    {
        Type EntryType { get; }
        int Pump(DebugDb db, int budget);
    }

    private sealed class Drain<T> : IDrain where T : struct, IEntry
    {
        private readonly Subscriber<T> _subscriber = DebugBus.Subscribe<T>(Mode.All);

        public Type EntryType => typeof(T);

        public int Pump(DebugDb db, int budget)
        {
            var reader = _subscriber.Reader;
            var count = 0;
            while (count < budget && reader.TryRead(out var entry))
            {
                db.Append(entry);
                count++;
            }

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
    private bool _disposed;

    /// <summary>Raised on the pumping thread for every frame ingested.</summary>
    public event Action<Frame>? FrameIngested;

    public DebugDbIngest(DebugDb db)
    {
        _db = db;
        DebugTypeRegistry.Registered += OnTypeRegistered;
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
    /// become visible before the entries published ahead of it. Entries and frames travel
    /// on separate channels, so: take the frames queued now, drain every entry channel, and
    /// only then append the taken frames, and only once every entry channel ran empty
    /// (otherwise they stay pending until a later pump drains the backlog).
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
        var drainedEverything = true;
        foreach (var drain in Volatile.Read(ref _drains))
        {
            var count = drain.Pump(_db, budget);
            any |= count > 0;
            drainedEverything &= count < budget;
        }

        if (!drainedEverything)
            return any;

        foreach (var frame in _pendingFrames)
        {
            _db.AppendFrame(frame);
            FrameIngested?.Invoke(frame);
        }

        _pendingFrames.Clear();
        return any;
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
