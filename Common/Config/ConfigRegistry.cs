using System.Reflection;
using System.Text;
using Tomlet.Models;

namespace Tyr.Common.Config;

public static class ConfigRegistry
{
    internal static string ConvertName(string s) => s;

    private static string MapName(Type type) => $"{type.Namespace}.{type.Name}";

    public static Dictionary<string, Configurable> Configurables { get; set; } = null!;

    public static Configurable Get(object obj) => Configurables[MapName(obj.GetType())];
    public static Configurable Get(Type type) => Configurables[MapName(type)];
    public static Configurable Get<T>() => Configurables[MapName(typeof(T))];

    public static event Action? OnAnyUpdated;

    public static void Initialize()
    {
        Configurables = AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => assembly.GetName().Name != null && assembly.GetName().Name!.StartsWith("Tyr"))
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.GetCustomAttribute<ConfigurableAttribute>() != null)
            .ToDictionary(MapName, type => new Configurable(type));

        Logger.ZLogTrace($"Found {Configurables.Count} configurables");
        foreach (var configurable in Configurables.Values)
        {
            Logger.ZLogTrace($" - {configurable.TypeName} @ {configurable.Namespace}");
        }

        foreach (var configurable in Configurables.Values)
        {
            configurable.OnUpdated += OnAnyUpdated;
        }
    }

    private static string ConvertPath(string path)
    {
        var parts = path.Split('.').Skip(1);
        var sb = new StringBuilder();
        foreach (var part in parts)
        {
            sb.Append(ConvertName(part));
            sb.Append('.');
        }

        sb.Remove(sb.Length - 1, 1);

        return sb.ToString();
    }

    public static TomlDocument ToToml()
    {
        var document = TomlDocument.CreateEmpty();

        foreach (var configurable in Configurables.Values)
        {
            var path = ConvertPath($"{configurable.Namespace}.{configurable.TypeName}");
            document.Put(path, configurable);
        }

        return document;
    }

    public static void FromToml(TomlDocument document)
    {
        foreach (var configurable in Configurables.Values)
        {
            var path = ConvertPath($"{configurable.Namespace}.{configurable.TypeName}");

            if (!document.TryGetValue(path, out var value) ||
                value is not TomlTable table)
                continue;

            configurable.FromToml(table);
        }
    }
}