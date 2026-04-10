using Tyr.Common.Debug.Db;

namespace Tyr.Gui.Data;

public sealed class PlaybackSessionManager : IDisposable
{
    private readonly IDebugDb _liveDb;
    private readonly SwitchableDebugDb _playbackDb;
    private readonly string _sessionRootDirectory;
    private readonly List<IDebugDb> _retainedDbs = [];

    private IDebugDb? _openedDb;
    private PlaybackSessionInfo? _openedSession;
    private IReadOnlyList<PlaybackSessionInfo> _sessions = [];

    public PlaybackSessionManager(IDebugDb liveDb, string sessionRootDirectory)
    {
        _liveDb = liveDb;
        _sessionRootDirectory = sessionRootDirectory;
        _playbackDb = new SwitchableDebugDb(liveDb);
        RefreshSessions();
    }

    public IDebugDb PlaybackDb => _playbackDb;
    public bool UsingLive => _openedDb is null;
    public string? CurrentSessionMetadataPath => _openedSession?.MetadataPath;
    public string CurrentSourceLabel => UsingLive ? "Live" : _openedSession?.DisplayName ?? "Opened Session";
    public IReadOnlyList<PlaybackSessionInfo> Sessions => _sessions;

    public void RefreshSessions()
    {
        if (!Directory.Exists(_sessionRootDirectory))
        {
            _sessions = [];
            return;
        }

        _sessions = Directory.GetFiles(_sessionRootDirectory, "session.json", SearchOption.AllDirectories)
            .Select(TryLoadSession)
            .OfType<PlaybackSessionInfo>()
            .OrderByDescending(static session => session.Metadata.CreatedAtUtc)
            .ToArray();
    }

    public void SwitchToLive()
    {
        _playbackDb.SetSource(_liveDb);
        RetainForDispose(_openedDb);
        _openedDb = null;
        _openedSession = null;
    }

    public void OpenSession(PlaybackSessionInfo session)
    {
        if (_openedSession?.MetadataPath == session.MetadataPath)
            return;

        var db = new DebugDb(session.DatabaseDirectory)
            .RegisterType<Tyr.Common.Debug.Logging.Entry>()
            .RegisterType<Tyr.Common.Debug.Plotting.Command>()
            .RegisterType<Tyr.Common.Debug.Drawing.Command>();

        var readOnlyDb = new ReadOnlyDebugDb(db);
        _playbackDb.SetSource(readOnlyDb);

        RetainForDispose(_openedDb);
        _openedDb = readOnlyDb;
        _openedSession = session;
    }

    public void Dispose()
    {
        _openedDb?.Dispose();
        foreach (var db in _retainedDbs)
            db.Dispose();
    }

    private void RetainForDispose(IDebugDb? db)
    {
        if (db is not null)
            _retainedDbs.Add(db);
    }

    private static PlaybackSessionInfo? TryLoadSession(string metadataPath)
    {
        try
        {
            var metadata = DebugDbSessionMetadata.Load(metadataPath);
            var sessionDirectory = Path.GetDirectoryName(metadataPath) ?? metadata.ResolveSessionDirectory(metadataPath);
            var databaseDirectory = metadata.ResolveDatabaseDirectory(metadataPath);
            if (!Directory.Exists(databaseDirectory))
                return null;

            return new PlaybackSessionInfo(metadata, metadataPath, sessionDirectory, databaseDirectory);
        }
        catch
        {
            return null;
        }
    }
}

public sealed record PlaybackSessionInfo(
    DebugDbSessionMetadata Metadata,
    string MetadataPath,
    string SessionDirectory,
    string DatabaseDirectory)
{
    public string DisplayName
    {
        get
        {
            var title = string.IsNullOrWhiteSpace(Metadata.CaptureLabel)
                ? Metadata.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                : Metadata.CaptureLabel!;

            return $"{title} [{Metadata.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm}]";
        }
    }
}
