using Tomlet;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Config;

[Configurable]
public sealed partial class Storage : IDisposable
{
    [ConfigEntry] private static int MaxLoadAttempts { get; set; } = 10;
    [ConfigEntry] private static DeltaTime LoadAttemptsDelay { get; set; } = DeltaTime.FromSeconds(0.1f);
    [ConfigEntry] private static DeltaTime DebounceDelay { get; set; } = DeltaTime.FromSeconds(0.2f);

    public string Path { get; }
    public StorageType StorageType { get; }

    private bool _loading;

    private readonly FileSystemWatcher? _watcher;
    private DateTime _lastReadTime;

    private readonly Debouncer _loadDebouncer;
    private readonly Debouncer _saveDebouncer;

    static Storage()
    {
        VectorTomlMappers.Register();
    }

    public Storage(string path, StorageType storageType)
    {
        Path = path;
        StorageType = storageType;

        Registry.OnAnyUpdated += OnOnAnyConfigUpdated;

        // setup the file watcher
        var fullPath = System.IO.Path.GetFullPath(Path);
        var directory = System.IO.Path.GetDirectoryName(fullPath)!;
        var filename = System.IO.Path.GetFileName(fullPath);

        _loadDebouncer = new Debouncer(DebounceDelay, Load);
        _saveDebouncer = new Debouncer(DebounceDelay, Save);

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

    private void OnFileChanged()
    {
        var newWriteTime = File.GetLastWriteTimeUtc(Path);
        if (newWriteTime <= _lastReadTime) return;

        Log.ZLogTrace($"Detected external changes to {StorageType} config file {Path}");
        _loadDebouncer.Trigger();
    }

    public void Load()
    {
        if (!File.Exists(Path))
        {
            Log.ZLogError($"{StorageType} config file {Path} does not exist");
            return;
        }

        for (var attempt = 1; attempt <= MaxLoadAttempts; attempt++)
        {
            try
            {
                var toml = TomlParser.ParseFile(Path);

                _loading = true;
                Registry.FromToml(toml, StorageType);
                _loading = false;

                _lastReadTime = File.GetLastWriteTimeUtc(Path);

                Log.ZLogTrace($"Loaded {StorageType} config file {Path}");

                return;
            }
            catch (IOException) when (attempt < MaxLoadAttempts)
            {
                Thread.Sleep(LoadAttemptsDelay.ToTimeSpan());
            }
            catch (Exception e)
            {
                Log.ZLogError(e, $"Failed to load {StorageType} config file {Path}");
            }
        }

        Log.ZLogError($"Failed to load {StorageType} config file {Path} after {MaxLoadAttempts} attempts.");
    }

    public void Save()
    {
        try
        {
            var toml = Registry.ToToml(StorageType);
            File.WriteAllText(Path, toml.SerializedValue);

            _lastReadTime = File.GetLastWriteTimeUtc(Path);

            Log.ZLogTrace($"Saved {StorageType} config file {Path}");
        }
        catch (Exception e)
        {
            Log.ZLogError(e, $"Failed to save {StorageType} config file {Path}");
        }
    }

    private void OnOnAnyConfigUpdated(StorageType storageType)
    {
        if (storageType != StorageType) return;

        if (_loading) return;

        Log.ZLogTrace($"Detected runtime changes to {StorageType} configs");

        _saveDebouncer.Trigger();
    }

    public void Dispose()
    {
        _watcher?.Dispose();
        _loadDebouncer.Dispose();
        _saveDebouncer.Dispose();
    }
}