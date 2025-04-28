using Tomlet;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Config;

[Configurable]
public static class Storage
{
    public static string Path { get; private set; } = string.Empty;

    private static bool _loading;

    private static FileSystemWatcher? _watcher;
    private static DateTime _lastReadTime;

    [ConfigEntry] private static float DebounceDelayS { get; set; } = 0.2f;
    private static readonly Debouncer LoadDebouncer = new(DeltaTime.FromSeconds(DebounceDelayS), Load);
    private static readonly Debouncer SaveDebouncer = new(DeltaTime.FromSeconds(DebounceDelayS), Save);

    public static void Initialize(string path)
    {
        Path = path;
        Registry.OnAnyUpdated += OnOnAnyConfigUpdated;

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

        Log.ZLogTrace($"Detected external changes to config file {Path}");
        LoadDebouncer.Trigger();
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
                Registry.FromToml(toml);
                _loading = false;

                _lastReadTime = File.GetLastWriteTimeUtc(Path);

                Log.ZLogTrace($"Loaded config file {Path}");

                return;
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                Thread.Sleep(delayMs);
            }
            catch (Exception e)
            {
                Log.ZLogError(e, $"Failed to load config file {Path}");
            }
        }

        Log.ZLogError($"Failed to load config file {Path} after {maxAttempts} attempts.");
    }

    public static void Save()
    {
        try
        {
            var toml = Registry.ToToml();
            File.WriteAllText(Path, toml.SerializedValue);

            _lastReadTime = File.GetLastWriteTimeUtc(Path);

            Log.ZLogTrace($"Saved config file {Path}");
        }
        catch (Exception e)
        {
            Log.ZLogError(e, $"Failed to save config file {Path}");
        }
    }

    private static void OnOnAnyConfigUpdated()
    {
        if (_loading) return;

        Log.ZLogTrace($"Detected runtime changes to configs");

        SaveDebouncer.Trigger();
    }
}