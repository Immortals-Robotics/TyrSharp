using System.Reflection;

namespace Tyr.Common.Config.New;

public class Configurable
{
    public event Action? OnUpdated;

    public readonly Type Type;

    public string Namespace => Type.Namespace ?? "Global";
    public string TypeName => Type.Name;

    public ConfigurableAttribute Meta;

    private readonly ConfigEntry[] _entries;

    public IEnumerable<ConfigEntry> Entries => _entries;

    private static Configurable[] _configurables;

    static Configurable()
    {
        _configurables = AppDomain.CurrentDomain.GetAssemblies()
            .Where(assembly => assembly.GetName().Name != null && assembly.GetName().Name!.StartsWith("Tyr"))
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttribute<ConfigurableAttribute>() != null)
            .Select(type => new Configurable(type))
            .ToArray();
    }

    public Configurable(Type type)
    {
        Type = type;
        Meta = type.GetCustomAttribute<ConfigurableAttribute>()!;

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

    public static IEnumerable<Configurable> AllInDomain => _configurables;
}