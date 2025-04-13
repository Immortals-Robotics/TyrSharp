using Tomlyn.Helpers;

namespace Tyr.Common.Config.New;

public static class ConfigSerialization
{
    private static readonly Func<string, string> ConvertName = TomlNamingHelper.PascalToSnakeCase;

    // A nested tree containing ConfigEntries that can be used for serialization
    public static IDictionary<string, object> Tree { get; }

    static ConfigSerialization()
    {
        Tree = new Dictionary<string, object>();

        foreach (var configurable in Configurable.AllInDomain)
        {
            var namespaceParts = configurable.Namespace.Split('.').Skip(1);
            var current = Tree;

            foreach (var namespacePart in namespaceParts)
            {
                var convertedName = ConvertName(namespacePart);
                if (!current.TryGetValue(convertedName, out var child))
                    current[convertedName] = child = new Dictionary<string, object>();
                current = (Dictionary<string, object>)child!;
            }

            var entries = configurable.Entries
                .ToDictionary(entry => ConvertName(entry.Name), object (entry) => entry);

            current[ConvertName(configurable.TypeName)] = entries;
        }
    }


    // Loads values from a config tree into entries
    public static void ApplyConfig(IDictionary<string, object> config)
    {
        ApplyConfig(config, Tree);
    }

    private static void ApplyConfig(IDictionary<string, object> currentConfig, IDictionary<string, object> currentTree)
    {
        foreach (var (key, configValue) in currentConfig)
        {
            if (!currentTree.TryGetValue(key, out var internalValue))
                continue;

            switch (configValue)
            {
                case IDictionary<string, object> configSubTree
                    when internalValue is IDictionary<string, object> internalSubTree:
                    ApplyConfig(configSubTree, internalSubTree);
                    break;

                case not null when internalValue is ConfigEntry entry:
                    try
                    {
                        entry.Value = configValue;
                    }
                    catch (Exception e)
                    {
                        Logger.ZLogError(e, $"Failed to apply value to '{entry.Name}': {e.Message}");
                    }

                    break;

                default:
                    Logger.ZLogWarning($"Ignoring config {key} due to type mismatch");
                    break;
            }
        }
    }
}