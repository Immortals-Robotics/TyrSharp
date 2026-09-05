using Tomlet.Models;

namespace Tyr.Common.Config;

/// <summary>
/// Runtime handle for a <c>[Configurable]</c> type: its entries, change notification and TOML conversion.
/// Instances are created by the generated <c>Configurable</c> property of each configurable type.
/// </summary>
public sealed class Configurable
{
    /// <summary>
    /// Raised after one or more entries of the given storage type changed. Fires on the thread that
    /// made the change, which is the config file watcher thread for reloads from disk. Handlers that
    /// must run on a specific thread should poll <see cref="Version"/> from that thread instead.
    /// </summary>
    public event Action<StorageType>? OnUpdated;

    public Type Type { get; }
    public string Name => Type.Name;
    public string Namespace => Type.Namespace ?? "Tyr.Global";
    public string? Description { get; }

    private readonly ConfigEntry[] _entries;
    public IReadOnlyList<ConfigEntry> Entries => _entries;

    private int _version;
    private int _batchDepth;
    private int _pendingMask;
    private int _suppressDepth;

    /// <summary>Incremented on every change of any entry. Compare against a stored value to poll for changes.</summary>
    public int Version => Volatile.Read(ref _version);

    public Configurable(Type type, string? description, ConfigEntry[] entries)
    {
        Type = type;
        Description = description;
        _entries = entries;

        foreach (var entry in entries)
        {
            entry.Owner = this;
        }
    }

    public ConfigEntry? Find(string name)
    {
        foreach (var entry in _entries)
        {
            if (entry.Name == name) return entry;
        }

        return null;
    }

    /// <summary>
    /// Records a change of an entry with the given storage type. Notifications are raised immediately,
    /// or once at the end of the outermost <see cref="BeginBatch"/> scope, or not at all inside a
    /// <see cref="SuppressNotifications"/> scope.
    /// </summary>
    public void MarkChanged(StorageType storageType)
    {
        if (Volatile.Read(ref _suppressDepth) > 0) return;

        Interlocked.Increment(ref _version);

        if (Volatile.Read(ref _batchDepth) > 0)
        {
            Interlocked.Or(ref _pendingMask, 1 << (int)storageType);
            return;
        }

        Raise(storageType);
    }

    /// <summary>
    /// Coalesces change notifications until the returned scope is disposed:
    /// every storage type that changed inside the scope is notified exactly once.
    /// </summary>
    public BatchScope BeginBatch()
    {
        Interlocked.Increment(ref _batchDepth);
        return new BatchScope(this);
    }

    private void EndBatch()
    {
        if (Interlocked.Decrement(ref _batchDepth) != 0) return;

        var mask = Interlocked.Exchange(ref _pendingMask, 0);
        for (var bit = 0; mask != 0; bit++, mask >>= 1)
        {
            if ((mask & 1) != 0) Raise((StorageType)bit);
        }
    }

    private void Raise(StorageType storageType)
    {
        Log.ZLogTrace($"Configurable {Type.FullName} updated ({storageType}).");
        OnUpdated?.Invoke(storageType);
        Registry.NotifyUpdated(storageType);
    }

    /// <summary>
    /// Suppresses change notification for the duration of the returned scope: setters still assign,
    /// but no version bump, no <see cref="OnUpdated"/> and no registry notification happen, so an
    /// attached <see cref="Storage"/> never sees the write and does not persist it.
    /// <para>
    /// Use this for entries the process re-derives at runtime from an external source (e.g. ball
    /// parameters taken from the SSL vision geometry packet), which must not overwrite the tuned
    /// value on disk. The GUI editor and the TOML load path deliberately do not use it.
    /// </para>
    /// <para>
    /// The scope is per configurable and process-global, not thread-local: a concurrent write to the
    /// same configurable from another thread is suppressed as well. Keep scopes short and on one thread.
    /// </para>
    /// </summary>
    public SuppressScope SuppressNotifications()
    {
        Interlocked.Increment(ref _suppressDepth);
        return new SuppressScope(this);
    }

    private void EndSuppress() => Interlocked.Decrement(ref _suppressDepth);

    public readonly struct BatchScope(Configurable owner) : IDisposable
    {
        public void Dispose() => owner.EndBatch();
    }

    public readonly struct SuppressScope(Configurable owner) : IDisposable
    {
        public void Dispose() => owner.EndSuppress();
    }

    public void SetDefaults()
    {
        using (BeginBatch())
        {
            foreach (var entry in _entries)
            {
                entry.ResetToDefault();
            }
        }
    }

    public TomlTable ToToml(StorageType storageType)
    {
        var table = new TomlTable();
        table.Comments.PrecedingComment = Description;

        foreach (var entry in _entries)
        {
            if (entry.StorageType != storageType) continue;

            try
            {
                var value = entry.ToToml();
                if (value is null) continue;

                table.PutValue(entry.Name, value);
            }
            catch (Exception exception)
            {
                Log.ZLogError(exception,
                    $"Failed to serialize {entry.StorageType} config entry {entry.Name} of type {entry.Type} to TOML");
            }
        }

        return table;
    }

    public void FromToml(TomlTable table, StorageType storageType)
    {
        using (BeginBatch())
        {
            foreach (var entry in _entries)
            {
                if (entry.StorageType != storageType) continue;
                if (!table.TryGetValue(entry.Name, out var value)) continue;

                try
                {
                    entry.FromToml(value);
                }
                catch (Exception exception)
                {
                    Log.ZLogError(exception,
                        $"Failed to parse {entry.StorageType} config entry {entry.Name} of type {entry.Type} from TOML");
                }
            }
        }
    }
}
