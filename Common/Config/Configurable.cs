using System.Reflection;
using Tomlet.Models;

namespace Tyr.Common.Config;

public class Configurable
{
    public event Action<StorageType>? OnUpdated;

    public readonly Type Type;

    public string Namespace => Type.Namespace ?? "Tyr.Global";

    public string? Description => _meta.Description;

    private readonly ConfigurableAttribute _meta;

    private readonly ConfigEntry[] _entries;

    public IEnumerable<ConfigEntry> Entries => _entries;

    internal Configurable(Type type)
    {
        Type = type;
        _meta = type.GetCustomAttribute<ConfigurableAttribute>()!;

        _entries = Type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(info => info.GetCustomAttribute<ConfigEntryAttribute>() != null)
            .Select(info => new ConfigEntry(info, this))
            .ToArray();
    }

    public void MarkChanged(StorageType storageType)
    {
        Log.ZLogTrace($"Configurable of type {Type.FullName} was updated.");
        OnUpdated?.Invoke(storageType);
    }

    public void SetDefaults()
    {
        foreach (var entry in Entries)
        {
            entry.Value = entry.DefaultValue;
        }
    }

    public TomlTable ToToml(StorageType storageType)
    {
        var table = new TomlTable();
        table.Comments.PrecedingComment = Description;

        foreach (var entry in Entries)
        {
            if (entry.StorageType != storageType) continue;

            table.Put(Registry.ConvertName($"{entry.Name}"), entry);
        }

        return table;
    }

    public void FromToml(TomlTable table, StorageType storageType)
    {
        foreach (var entry in Entries)
        {
            if (entry.StorageType != storageType) continue;

            var key = Registry.ConvertName($"{entry.Name}");
            if (!table.TryGetValue(key, out var value)) continue;

            entry.FromToml(value);
        }
    }
}