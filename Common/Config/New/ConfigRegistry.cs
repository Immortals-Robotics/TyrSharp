using System.Reflection;
using System.Text;
using Tomlet.Models;
using Tomlyn.Helpers;

namespace Tyr.Common.Config.New;

public static class ConfigRegistry
{
    internal static readonly Func<string, string> ConvertName = TomlNamingHelper.PascalToSnakeCase;

    private static string MapName(Type type) => $"{type.Namespace}.{type.Name}";

    private static Dictionary<string, Configurable>? _configurables;

    public static Dictionary<string, Configurable> Configurables => _configurables ??= AppDomain.CurrentDomain
        .GetAssemblies()
        .Where(assembly => assembly.GetName().Name != null && assembly.GetName().Name!.StartsWith("Tyr"))
        .SelectMany(assembly => assembly.GetTypes())
        .Where(type => type.GetCustomAttribute<ConfigurableAttribute>() != null)
        .ToDictionary(MapName, type => new Configurable(type));

    public static Configurable Get(object obj) => Configurables[MapName(obj.GetType())];
    public static Configurable Get(Type type) => Configurables[MapName(type)];
    public static Configurable Get<T>() => Configurables[MapName(typeof(T))];

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