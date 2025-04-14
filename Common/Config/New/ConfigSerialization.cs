using System.Text;
using Tomlet;
using Tomlet.Models;
using Tomlyn.Helpers;

namespace Tyr.Common.Config.New;

public static class ConfigSerialization
{
    internal static readonly Func<string, string> ConvertName = TomlNamingHelper.PascalToSnakeCase;

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

    static ConfigSerialization()
    {
        TomletMain.RegisterMapper<ConfigEntry>(entry => entry?.ToToml(), null);
        TomletMain.RegisterMapper<Configurable>(configurable => configurable?.ToToml(), null);
    }
    
    public static TomlDocument ToToml()
    {
        var document = TomlDocument.CreateEmpty();

        foreach (var configurable in Configurable.AllInDomain)
        {
            var path = ConvertPath($"{configurable.Namespace}.{configurable.TypeName}");
            document.Put(path, configurable);
        }

        return document;
    }

    // Loads values from a config tree into entries
    public static void ApplyConfig(IDictionary<string, object> config)
    {
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