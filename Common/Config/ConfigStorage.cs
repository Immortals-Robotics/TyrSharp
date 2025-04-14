using Tomlet;

namespace Tyr.Common.Config;

public static class ConfigStorage
{
    public static string Path { get; private set; } = string.Empty;

    private static bool _loading = false;

    public static void Initialize(string path)
    {
        Path = path;
        ConfigRegistry.OnAnyUpdated += OnOnAnyConfigUpdated;
    }

    public static void Load()
    {
        try
        {
            var toml = TomlParser.ParseFile(Path);

            _loading = true;
            ConfigRegistry.FromToml(toml);
            _loading = false;
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Failed to load config file {Path}");
        }
    }

    public static void Save()
    {
        try
        {
            var toml = ConfigRegistry.ToToml();
            File.WriteAllText(Path, toml.SerializedValue);
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Failed to save config file {Path}");
        }
    }

    private static void OnOnAnyConfigUpdated()
    {
        if (_loading) return;

        Save();
    }
}