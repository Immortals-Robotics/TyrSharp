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

    public static IEnumerable<Configurable> GetAllConfigurables()
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            var name = assembly.GetName().Name;
            if (name == null || !name.StartsWith("Tyr."))
                continue;

            foreach (var type in assembly.GetTypes())
            {
                if (type.GetCustomAttribute<ConfigurableAttribute>() == null) continue;

                yield return new Configurable(type);
            }
        }
    }
}