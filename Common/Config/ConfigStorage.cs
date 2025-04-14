using Tomlet;

namespace Tyr.Common.Config;

public static class ConfigStorage
{
    public static string Path { get; private set; } = string.Empty;

    private static bool _loading;

    private static FileSystemWatcher? _watcher;
    private static DateTime _lastReadTime;

    public static void Initialize(string path)
    {
        Path = path;
        ConfigRegistry.OnAnyUpdated += OnOnAnyConfigUpdated;

        // setup the file watcher
        var fullPath = System.IO.Path.GetFullPath(Path);
        var directory = System.IO.Path.GetDirectoryName(fullPath)!;
        var filename = System.IO.Path.GetFileName(fullPath);

        _watcher = new FileSystemWatcher(directory, filename)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
            EnableRaisingEvents = true
        };

        _watcher.Changed += (_, _) => OnFileChanged();

        // load changes made when we were not running
        Load();

        // and write back any changes to the config definitions
        Save();
    }

    private static void OnFileChanged()
    {
        var newWriteTime = File.GetLastWriteTimeUtc(Path);
        if (newWriteTime <= _lastReadTime) return;

        Logger.ZLogTrace($"Loading external changes to config file {Path}");
        Load();
    }

    public static void Load()
    {
        const int maxAttempts = 10;
        const int delayMs = 100;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                var toml = TomlParser.ParseFile(Path);

                _loading = true;
                ConfigRegistry.FromToml(toml);
                _loading = false;

                _lastReadTime = File.GetLastWriteTimeUtc(Path);

                Logger.ZLogTrace($"Loaded config file {Path}");

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
            catch (Exception e)
            {
                Logger.ZLogError(e, $"Failed to load config file {Path}");
            }
        }

        Logger.ZLogError($"Failed to load config file {Path} after {maxAttempts} attempts.");
    }

    public static void Save()
    {
        try
        {
            var toml = ConfigRegistry.ToToml();
            File.WriteAllText(Path, toml.SerializedValue);

            _lastReadTime = File.GetLastWriteTimeUtc(Path);

            Logger.ZLogTrace($"Saved config file {Path}");
        }
        catch (Exception e)
        {
            Logger.ZLogError(e, $"Failed to save config file {Path}");
        }
    }

    private static void OnOnAnyConfigUpdated()
    {
        if (_loading) return;

        Logger.ZLogTrace($"Saving runtime changes to config file {Path}");

        Save();
    }
}