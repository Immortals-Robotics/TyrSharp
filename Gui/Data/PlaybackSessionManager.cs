using System.IO.Compression;
using System.Diagnostics;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;

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
    private int _sourceRevision;

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
    public string SessionRootDirectory => _sessionRootDirectory;
    public int SourceRevision => _sourceRevision;

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
        _sourceRevision++;
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
        _sourceRevision++;
    }

    public void ExportSession(PlaybackSessionInfo session, string archivePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);
        if (File.Exists(archivePath))
            File.Delete(archivePath);

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        var baseDirectory = Path.GetDirectoryName(session.SessionDirectory) ?? session.SessionDirectory;
        foreach (var filePath in Directory.EnumerateFiles(session.SessionDirectory, "*", SearchOption.AllDirectories))
        {
            var entryName = Path.GetRelativePath(baseDirectory, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

            using var input = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            using var output = entry.Open();
            input.CopyTo(output);
        }
    }

    public void ImportSessionArchive(string archivePath)
    {
        Directory.CreateDirectory(_sessionRootDirectory);
        using var archive = ZipFile.OpenRead(archivePath);

        var firstEntry = archive.Entries
            .FirstOrDefault(static entry => !string.IsNullOrEmpty(entry.FullName) && !entry.FullName.EndsWith('/'));
        if (firstEntry is null)
            return;

        var topLevelDirectory = firstEntry.FullName
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();
        if (string.IsNullOrWhiteSpace(topLevelDirectory))
            return;

        var destinationRoot = Path.Combine(_sessionRootDirectory, topLevelDirectory);
        if (Directory.Exists(destinationRoot))
        {
            Log.ZLogWarning($"Skipping session import from {archivePath} because session id '{topLevelDirectory}' already exists.");
            return;
        }

        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.FullName))
                continue;

            var normalizedPath = entry.FullName.Replace('\\', '/');
            if (!normalizedPath.StartsWith(topLevelDirectory + "/", StringComparison.Ordinal) &&
                !string.Equals(normalizedPath, topLevelDirectory, StringComparison.Ordinal))
            {
                continue;
            }

            var relativePath = normalizedPath.Length == topLevelDirectory.Length
                ? string.Empty
                : normalizedPath[(topLevelDirectory.Length + 1)..];

            var destinationPath = string.IsNullOrEmpty(relativePath)
                ? destinationRoot
                : Path.Combine(destinationRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (entry.FullName.EndsWith('/'))
            {
                Directory.CreateDirectory(destinationPath);
                continue;
            }

            var destinationDirectory = Path.GetDirectoryName(destinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);

            using var input = entry.Open();
            using var output = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            input.CopyTo(output);
        }
    }

    public void RenameSession(PlaybackSessionInfo session, string? captureLabel)
    {
        var metadata = DebugDbSessionMetadata.Load(session.MetadataPath);
        metadata.CaptureLabel = captureLabel;
        metadata.Save(session.MetadataPath);
        if (_openedSession?.MetadataPath == session.MetadataPath)
            _openedSession = session with { Metadata = metadata };
    }

    public void AssignCaptureLabel(IEnumerable<PlaybackSessionInfo> sessions, string? captureLabel)
    {
        foreach (var session in sessions)
            RenameSession(session, captureLabel);
    }

    public void DeleteSessions(IEnumerable<PlaybackSessionInfo> sessions)
    {
        foreach (var session in sessions)
        {
            if (_openedSession?.MetadataPath == session.MetadataPath)
                SwitchToLive();

            if (Directory.Exists(session.SessionDirectory))
                Directory.Delete(session.SessionDirectory, recursive: true);
        }
    }

    public void RevealInExplorer(PlaybackSessionInfo session)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"\"{session.SessionDirectory}\"",
            UseShellExecute = true,
        });
    }

    public void Dispose()
    {
        _openedDb?.Dispose();
        foreach (var db in _retainedDbs)
            db.Dispose();
    }

    private void RetainForDispose(IDebugDb? db)
    {
        if (db is not null && !ReferenceEquals(db, _liveDb))
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
    Tyr.Common.Debug.Db.DebugDbSessionMetadata Metadata,
    string MetadataPath,
    string SessionDirectory,
    string DatabaseDirectory)
{
    public string RangeLabel
    {
        get
        {
            if (Metadata.FirstFrameTimestamp.HasValue && Metadata.LastFrameTimestamp.HasValue)
            {
                var frameSpan = Metadata.LastFrameTimestamp.Value - Metadata.FirstFrameTimestamp.Value;
                if (frameSpan > DeltaTime.Zero)
                    return $"{frameSpan.Seconds:0.###} s";
            }

            if (Metadata.ClosedAtUtc.HasValue)
            {
                var wallClockSpan = Metadata.ClosedAtUtc.Value - Metadata.CreatedAtUtc;
                if (wallClockSpan > TimeSpan.Zero)
                    return $"{wallClockSpan.TotalSeconds:0.###} s";
            }

            return "n/a";
        }
    }

    public string DisplayName
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Metadata.CaptureLabel))
                return Metadata.CaptureLabel!;

            return Metadata.SessionId;
        }
    }
}
