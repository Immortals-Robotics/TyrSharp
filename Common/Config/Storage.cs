using Tomlet;
using Tomlet.Models;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Config;

/// <summary>
/// Binds one storage type (project or user) to a TOML file: loads it, watches it for external edits,
/// and writes it back, debounced, whenever an entry of that storage type changes at runtime.
/// </summary>
[Configurable]
public sealed partial class Storage : IDisposable
{
    // These three are read in the constructor, before any file is loaded, so a change only applies on the next run.
    [ConfigEntry] private static partial int MaxLoadAttempts { get; set; } = 10;
    [ConfigEntry] private static partial DeltaTime LoadAttemptsDelay { get; set; } = DeltaTime.FromSeconds(0.1f);
    [ConfigEntry] private static partial DeltaTime DebounceDelay { get; set; } = DeltaTime.FromSeconds(0.2f);

    public string Path { get; }
    public StorageType StorageType { get; }

    private volatile bool _loading;
    private volatile bool _saving;

    // The last document parsed from or written to disk. Serves two purposes: values are replayed
    // from it onto configurables registered after the load, and saves merge into it so that tables
    // owned by modules that are not loaded in this process survive a rewrite of the file.
    private TomlDocument? _document;

    private readonly FileSystemWatcher _watcher;
    private DateTime _lastReadTime;

    private readonly Debouncer _loadDebouncer;
    private readonly Debouncer _saveDebouncer;

    public Storage(string path, StorageType storageType)
    {
        Path = path;
        StorageType = storageType;

        _loadDebouncer = new Debouncer(DebounceDelay, Load);
        _saveDebouncer = new Debouncer(DebounceDelay, Save);

        var fullPath = System.IO.Path.GetFullPath(Path);
        var directory = System.IO.Path.GetDirectoryName(fullPath)!;
        var filename = System.IO.Path.GetFileName(fullPath);

        Directory.CreateDirectory(directory);

        _watcher = new FileSystemWatcher(directory, filename)
        {
            NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
        };
        _watcher.Changed += (_, _) => OnFileChanged();

        Registry.AttachStorage(this);
        Registry.OnAnyUpdated += OnAnyConfigUpdated;

        // load changes made when we were not running
        Load();

        // and write back any changes to the config definitions
        Save();

        _watcher.EnableRaisingEvents = true;
    }

    private void OnFileChanged()
    {
        if (_saving) return;

        var newWriteTime = File.GetLastWriteTimeUtc(Path);
        if (newWriteTime <= _lastReadTime) return;

        Log.ZLogTrace($"Detected external changes to {StorageType} config file {Path}");
        _loadDebouncer.Trigger();
    }

    /// <summary>Applies the last loaded document to a configurable that registered after the load.</summary>
    internal void OnConfigurableRegistered(Configurable configurable)
    {
        var document = _document;
        if (document is not null)
        {
            _loading = true;
            try
            {
                Registry.Apply(configurable, document, StorageType);
            }
            finally
            {
                _loading = false;
            }
        }

        // the file on disk may lack this configurable's table, or carry stale entries for it
        _saveDebouncer.Trigger();
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
                var document = TomlParser.ParseFile(Path);

                _loading = true;
                try
                {
                    Registry.ReadToml(document, StorageType);
                }
                finally
                {
                    _loading = false;
                }

                _document = document;
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
                return;
            }
        }

        Log.ZLogError($"Failed to load {StorageType} config file {Path} after {MaxLoadAttempts} attempts.");
    }

    public void Save()
    {
        try
        {
            var document = _document ?? TomlDocument.CreateEmpty();
            Registry.WriteToml(document, StorageType);
            var text = document.SerializedValue;

            _saving = true;
            try
            {
                File.WriteAllText(Path, text);
                _lastReadTime = File.GetLastWriteTimeUtc(Path);
            }
            finally
            {
                _saving = false;
            }

            _document = document;

            Log.ZLogTrace($"Saved {StorageType} config file {Path}");
        }
        catch (Exception e)
        {
            Log.ZLogError(e, $"Failed to save {StorageType} config file {Path}");
        }
    }

    private void OnAnyConfigUpdated(StorageType storageType)
    {
        if (storageType != StorageType) return;
        if (_loading) return;

        Log.ZLogTrace($"Detected runtime changes to {StorageType} configs");
        _saveDebouncer.Trigger();
    }

    public void Dispose()
    {
        Registry.OnAnyUpdated -= OnAnyConfigUpdated;
        Registry.DetachStorage(this);

        _watcher.Dispose();
        _loadDebouncer.Dispose();
        _saveDebouncer.Dispose();
    }
}
