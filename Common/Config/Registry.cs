using Tomlet.Models;

namespace Tyr.Common.Config;

/// <summary>
/// Global index of all registered configurables. Generated module initializers register every
/// <c>[Configurable]</c> type of their assembly here; <see cref="Storage"/> instances attach here
/// so that types registered after a storage was loaded still receive their persisted values.
/// </summary>
public static class Registry
{
    private static readonly Lock Sync = new();
    private static readonly Dictionary<Type, Configurable> ByType = [];
    private static readonly List<Storage> Storages = [];

    private static Configurable[] _all = [];
    private static int _version;

    /// <summary>All registered configurables, ordered by their TOML path. The array is replaced on registration, never mutated.</summary>
    public static IReadOnlyList<Configurable> Configurables => Volatile.Read(ref _all);

    private static ConfigTreeNode _tree = new("");

    /// <summary>Namespace tree of all registered configurables. Rebuilt and swapped on registration.</summary>
    public static ConfigTreeNode Tree => Volatile.Read(ref _tree);

    /// <summary>Incremented on every change of any entry of any configurable.</summary>
    public static int Version => Volatile.Read(ref _version);

    /// <summary>Raised after any configurable changed. See <see cref="Configurable.OnUpdated"/> for threading notes.</summary>
    public static event Action<StorageType>? OnAnyUpdated;

    public static Configurable Get(object obj) => Get(obj.GetType());
    public static Configurable Get<T>() => Get(typeof(T));

    public static Configurable Get(Type type)
    {
        if (TryGet(type, out var configurable)) return configurable;
        throw new KeyNotFoundException($"{type.FullName} is not a registered configurable.");
    }

    public static bool TryGet(Type type, out Configurable configurable)
    {
        lock (Sync)
        {
            return ByType.TryGetValue(type, out configurable!);
        }
    }

    /// <summary>
    /// Registers a configurable. Values already loaded by attached storages are applied to it immediately.
    /// Called from generated module initializers; safe to call for the same type again (replaces).
    /// </summary>
    public static void Register(Configurable configurable)
    {
        Storage[] storages;

        lock (Sync)
        {
            ByType[configurable.Type] = configurable;

            var all = ByType.Values.ToArray();
            Array.Sort(all, static (a, b) => string.CompareOrdinal(TomlPath(a), TomlPath(b)));
            Volatile.Write(ref _all, all);
            Volatile.Write(ref _tree, BuildTree(all));

            storages = Storages.ToArray();
        }

        Log.ZLogTrace($"Registered configurable {configurable.Type.FullName} with {configurable.Entries.Count} entries");

        foreach (var storage in storages)
        {
            storage.OnConfigurableRegistered(configurable);
        }
    }

    internal static void AttachStorage(Storage storage)
    {
        lock (Sync)
        {
            Storages.Add(storage);
        }
    }

    internal static void DetachStorage(Storage storage)
    {
        lock (Sync)
        {
            Storages.Remove(storage);
        }
    }

    internal static void NotifyUpdated(StorageType storageType)
    {
        Interlocked.Increment(ref _version);
        OnAnyUpdated?.Invoke(storageType);
    }

    /// <summary>Dotted TOML table path of a configurable: its namespace without the leading root segment, then the type name.</summary>
    public static string TomlPath(Configurable configurable)
    {
        var ns = configurable.Namespace;
        var dot = ns.IndexOf('.');
        var trimmed = dot >= 0 ? ns[(dot + 1)..] : ns;
        return $"{trimmed}.{configurable.Name}";
    }

    private static ConfigTreeNode BuildTree(Configurable[] all)
    {
        var root = new ConfigTreeNode("");

        foreach (var configurable in all)
        {
            var current = root;
            foreach (var part in TomlPath(configurable).Split('.'))
            {
                if (!current.Children.TryGetValue(part, out var child))
                {
                    child = new ConfigTreeNode(part);
                    current.Children[part] = child;
                }

                current = child;
            }

            current.Configurable = configurable;
        }

        return root;
    }

    /// <summary>
    /// Writes every registered configurable's entries of the given storage type into the document,
    /// replacing tables it owns and leaving everything else untouched.
    /// </summary>
    public static void WriteToml(TomlDocument document, StorageType storageType)
    {
        foreach (var configurable in Configurables)
        {
            var table = configurable.ToToml(storageType);
            if (table.Entries.Count == 0) continue;

            document.PutValue(TomlPath(configurable), table);
        }
    }

    /// <summary>Applies the document to every registered configurable.</summary>
    public static void ReadToml(TomlDocument document, StorageType storageType)
    {
        foreach (var configurable in Configurables)
        {
            Apply(configurable, document, storageType);
        }
    }

    internal static void Apply(Configurable configurable, TomlDocument document, StorageType storageType)
    {
        if (!document.TryGetValue(TomlPath(configurable), out var value) || value is not TomlTable table)
            return;

        configurable.FromToml(table, storageType);
    }
}
