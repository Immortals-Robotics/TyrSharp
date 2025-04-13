using System.Reflection;

namespace Tyr.Common.Config.New;

public class Configurable(Type type)
{
    public event Action OnUpdated;

    public readonly Type Type = type;

    public string Namespace => Type.Namespace ?? "Global";
    public string TypeName => Type.Name;

    public ConfigurableAttribute Meta = type.GetCustomAttribute<ConfigurableAttribute>()!;

    public void OnEntryChanged(ConfigEntry entry)
    {
        OnUpdated?.Invoke();
    }

    public IEnumerable<ConfigEntry> Entries()
    {
        return Type
            .GetMembers(BindingFlags.Public | BindingFlags.Static)
            .Where(info => info.GetCustomAttribute<ConfigEntryAttribute>() != null)
            .Select(info => new ConfigEntry(info, this));
    }

    public void SetDefaults()
    {
        foreach (var entry in Entries())
        {
            entry.Value = entry.DefaultValue;
        }
    }

    public static IEnumerable<Configurable> GetAllConfigurables() => Assembly
        .GetExecutingAssembly()
        .GetTypes()
        .Where(type => type.GetCustomAttribute<ConfigurableAttribute>() != null)
        .Select(info => new Configurable(info));
}