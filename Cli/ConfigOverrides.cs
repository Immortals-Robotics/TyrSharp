using Tomlet;
using Tomlet.Models;
using Tyr.Common.Config;

namespace Tyr.Cli;

/// <summary>
/// Applies "Path.To.Type.Entry = value" overrides to registered configurables without notifying,
/// so the attached storages never persist them and the run leaves the config files alone.
/// Must run before the modules are constructed: they read the entries in their constructors.
/// </summary>
public static class ConfigOverrides
{
    public static void Apply(IEnumerable<KeyValuePair<string, TomlValue>> overrides)
    {
        foreach (var (key, value) in overrides)
        {
            var (configurable, entry) = Resolve(key);

            using (configurable.SuppressNotifications())
            {
                entry.FromToml(value);
            }

            Log.ZLogInformation($"Config override {key} = {entry.Value}");
        }
    }

    /// <summary>Parses command-line values as TOML; anything that is not valid TOML is taken as a bare string.</summary>
    public static IEnumerable<KeyValuePair<string, TomlValue>> FromStrings(IEnumerable<KeyValuePair<string, string>> raw)
    {
        foreach (var (key, text) in raw)
        {
            yield return new KeyValuePair<string, TomlValue>(key, ParseValue(text));
        }
    }

    private static TomlValue ParseValue(string text)
    {
        try
        {
            return new TomlParser().Parse($"value = {text}").GetValue("value");
        }
        catch (Exception)
        {
            return new TomlString(text);
        }
    }

    private static (Configurable, ConfigEntry) Resolve(string key)
    {
        var dot = key.LastIndexOf('.');
        if (dot <= 0 || dot == key.Length - 1)
            throw new ArgumentException($"Config override key must look like Module.Type.Entry, got '{key}'");

        var path = key[..dot];
        var entryName = key[(dot + 1)..];

        foreach (var configurable in Registry.Configurables)
        {
            if (Registry.TomlPath(configurable) != path) continue;

            var entry = configurable.Find(entryName)
                        ?? throw new ArgumentException($"Configurable {path} has no entry '{entryName}'");
            return (configurable, entry);
        }

        throw new ArgumentException($"No configurable registered at '{path}'. " +
                                    "Paths are the TOML section names, e.g. Soccer.Runner or Sender.Simulator.");
    }
}
