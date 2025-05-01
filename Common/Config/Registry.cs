using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using Tomlet.Models;

namespace Tyr.Common.Config;

public static class Registry
{
    internal static string ConvertName(string s) => s;

    private static string MapName(Type type) => $"{type.Namespace}.{type.Name}";

    public static Dictionary<string, object> Tree { get; } = [];
    public static ConcurrentDictionary<string, Configurable> Configurables { get; } = [];

    public static Configurable Get(object obj) => Configurables[MapName(obj.GetType())];
    public static Configurable Get(Type type) => Configurables[MapName(type)];
    public static Configurable Get<T>() => Configurables[MapName(typeof(T))];

    public static event Action? OnAnyUpdated;

    public static void RegisterAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var configurables = assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<ConfigurableAttribute>() != null)
            .ToDictionary(MapName, type => new Configurable(type));

        if (configurables.Count == 0) return;
        Log.ZLogTrace($"Found {configurables.Count} configurables in {assembly.GetName().Name}");
        foreach (var configurable in configurables.Values)
        {
            Log.ZLogTrace($" - {configurable.Type.Name} @ {configurable.Namespace}");
        }

        // Merge with existing configurables instead of replacing
        foreach (var (key, configurable) in configurables)
        {
            if (Configurables.TryGetValue(key, out var existing))
                existing.OnUpdated -= OnConfigurableUpdated; // Remove old handler

            Configurables[key] = configurable;
            configurable.OnUpdated += OnConfigurableUpdated;
        }
        
        // rebuild the config tree
        RebuildTree();
    }

    private static void OnConfigurableUpdated()
    {
        OnAnyUpdated?.Invoke();
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

    private static void RebuildTree()
    {
        Tree.Clear();

        foreach (var (fullName, configurable) in Configurables) // fullName is "A.B.FirstType", etc.
        {
            var parts = fullName.Split('.');
            var namespaceParts = parts[1..^1];
            var typeName = parts[^1];

            var current = Tree;

            foreach (var part in namespaceParts)
            {
                if (!current.TryGetValue(part, out var child))
                {
                    child = new Dictionary<string, object>();
                    current[part] = child;
                }

                current = (Dictionary<string, object>)child;
            }

            current[typeName] = configurable;
        }
    }
    
    public static TomlDocument ToToml()
    {
        var document = TomlDocument.CreateEmpty();

        foreach (var configurable in Configurables.Values)
        {
            var path = ConvertPath($"{configurable.Namespace}.{configurable.Type.Name}");
            document.Put(path, configurable);
        }

        return document;
    }

    public static void FromToml(TomlDocument document)
    {
        foreach (var configurable in Configurables.Values)
        {
            var path = ConvertPath($"{configurable.Namespace}.{configurable.Type.Name}");

            if (!document.TryGetValue(path, out var value) ||
                value is not TomlTable table)
                continue;

            configurable.FromToml(table);
        }
    }
}