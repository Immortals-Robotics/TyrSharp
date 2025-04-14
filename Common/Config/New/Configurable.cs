using System.Reflection;
using Tomlet.Models;

namespace Tyr.Common.Config.New;

public class Configurable
{
    public event Action? OnUpdated;

    public readonly Type Type;

    public string Namespace => Type.Namespace ?? "Global";
    public string TypeName => Type.Name;

    public string? Comment => _meta.Comment;

    private readonly ConfigurableAttribute _meta;

    private readonly ConfigEntry[] _entries;

    public IEnumerable<ConfigEntry> Entries => _entries;

    private static string MapName(Type type) => $"{type.Namespace}.{type.Name}";

    private static Dictionary<string, Configurable>? _configurables;

    private static Dictionary<string, Configurable> Configurables => _configurables ??= AppDomain.CurrentDomain
        .GetAssemblies()
        .Where(assembly => assembly.GetName().Name != null && assembly.GetName().Name!.StartsWith("Tyr"))
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type.GetCustomAttribute<ConfigurableAttribute>() != null)
        .ToDictionary(MapName, type => new Configurable(type));

    public static IEnumerable<Configurable> AllInDomain => Configurables.Values;

    public static Configurable Get(object obj) => Configurables[MapName(obj.GetType())];
    public static Configurable Get(Type type) => Configurables[MapName(type)];
    public static Configurable Get<T>() => Configurables[MapName(typeof(T))];

    private Configurable(Type type)
    {
        Type = type;
        _meta = type.GetCustomAttribute<ConfigurableAttribute>()!;

        _entries = Type
            .GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Where(info => info.GetCustomAttribute<ConfigEntryAttribute>() != null)
            .Select(info => new ConfigEntry(info, this))
            .ToArray();
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
        if (Comment != null)
            table.Comments.PrecedingComment = Comment;

        foreach (var entry in Entries)
        {
            table.Put(ConfigSerialization.ConvertName($"{entry.Name}"), entry);
        }

        return table;
    }
}