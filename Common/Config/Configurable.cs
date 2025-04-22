using System.Reflection;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Config;

public class Configurable
{
    public event Action? OnUpdated;

    public readonly Type Type;

    public string Namespace => Type.Namespace ?? "Tyr.Global";
    public string TypeName => Type.Name;

    public string? Comment => _meta.Comment;

    private readonly ConfigurableAttribute _meta;

    private readonly ConfigEntry[] _entries;

    public IEnumerable<ConfigEntry> Entries => _entries;

    static Configurable()
    {
        TomletMain.RegisterMapper<Configurable>(configurable => configurable?.ToToml(), null);
    }

    internal Configurable(Type type)
    {
        Type = type;
        _meta = type.GetCustomAttribute<ConfigurableAttribute>()!;

        _entries = Type
            .GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .Where(info => info.GetCustomAttribute<ConfigEntryAttribute>() != null)
            .Select(info => new ConfigEntry(info, this))
            .ToArray();

        OnUpdated += () => Logger.ZLogTrace($"Configurable of type {Type.FullName} was updated.");
    }

    public void OnEntryChanged(ConfigEntry entry)
    {
        OnUpdated?.Invoke();
    }

    public void SetDefaults()
    {
        foreach (var entry in Entries)
        {
            entry.Value = entry.DefaultValue;
        }
    }

    public TomlTable ToToml()
    {
        var table = new TomlTable();
        table.Comments.PrecedingComment = Comment;

        foreach (var entry in Entries)
        {
            table.Put(Registry.ConvertName($"{entry.Name}"), entry);
        }

        return table;
    }

    public void FromToml(TomlTable table)
    {
        foreach (var entry in Entries)
        {
            var key = Registry.ConvertName($"{entry.Name}");
            if (!table.TryGetValue(key, out var value)) continue;

            entry.FromToml(value);
        }
    }
}