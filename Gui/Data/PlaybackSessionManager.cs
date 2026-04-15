using System.IO.Compression;
using System.Diagnostics;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;
using Tyr.Gui.Views;

namespace Tyr.Gui.Data;

public sealed class PlaybackSessionManager : IDisposable
{
    private readonly IDebugDb _liveDb;
    private readonly string _liveDatabaseDirectory;
    private readonly SwitchableDebugDb _playbackDb;
    private readonly string _sessionRootDirectory;
    private readonly string _tempRoot;
    private readonly List<IDebugDb> _retainedDbs = [];
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();

    private IDebugDb? _openedDb;
    private PlaybackSessionInfo? _openedSession;
    private string? _openedTempDirectory;
    private IReadOnlyList<PlaybackSessionInfo> _sessions = [];
    private int _sourceRevision;

    public PlaybackSessionManager(IDebugDb liveDb, string liveDatabaseDirectory, string sessionRootDirectory)
    {
        _liveDb = liveDb;
        _liveDatabaseDirectory = liveDatabaseDirectory;
        _sessionRootDirectory = sessionRootDirectory;
        _tempRoot = Path.Combine(Path.GetTempPath(), "TyrSharp", "Sessions");
        _playbackDb = new SwitchableDebugDb(liveDb);

        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);

        RefreshSessions();

        // Dedicated low-priority thread for background I/O
        var thread = new Thread(BackgroundLoop)
        {
            Name = "PlaybackSessionManager-BackgroundLoop",
            IsBackground = true,
            Priority = ThreadPriority.Lowest
        };
        thread.Start();
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
        lock (_lock)
        {
            if (!Directory.Exists(_sessionRootDirectory))
            {
                _sessions = [];
                return;
            }

            var livePath = Path.GetFullPath(_liveDatabaseDirectory);
            var uncompacted = Directory.EnumerateFiles(_sessionRootDirectory, "session.json", SearchOption.AllDirectories)
                .Select(TryLoadSession)
                .OfType<PlaybackSessionInfo>()
                .Where(s => !string.Equals(Path.GetFullPath(s.DatabaseDirectory), livePath, StringComparison.OrdinalIgnoreCase));

            var compacted = Directory.EnumerateFiles(_sessionRootDirectory, "*.tyrlog", SearchOption.AllDirectories)
                .Select(TryLoadCompactSession)
                .OfType<PlaybackSessionInfo>();

            _sessions = uncompacted.Concat(compacted)
                .OrderByDescending(static session => session.Metadata.CreatedAtUtc)
                .ToArray();
        }
    }

    public void SwitchToLive()
    {
        _playbackDb.SetSource(_liveDb);
        RetainForDispose(_openedDb);
        _openedDb = null;
        _openedSession = null;

        if (_openedTempDirectory != null && Directory.Exists(_openedTempDirectory))
        {
            try { Directory.Delete(_openedTempDirectory, recursive: true); } catch { /* ignore */ }
            _openedTempDirectory = null;
        }

        _sourceRevision++;
    }

    public void OpenSession(PlaybackSessionInfo session)
    {
        if (_openedSession?.MetadataPath == session.MetadataPath)
            return;

        var databaseDirectory = session.DatabaseDirectory;
        string? tempDir = null;

        if (session.IsCompacted && session.ArchivePath != null)
        {
            // Each open gets a unique extraction directory so we never conflict with
            // a previously retained DebugDb that still holds file handles on the same session.
            // _tempRoot is wiped on startup, so stale dirs from prior runs are cleaned up.
            tempDir = Path.Combine(_tempRoot, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            ZipFile.ExtractToDirectory(session.ArchivePath, tempDir);
            databaseDirectory = Path.Combine(tempDir, session.Metadata.SessionId, "db");
        }

        var db = new DebugDb(databaseDirectory)
            .RegisterKnownTypes();
        db.BuildJournal(LogView.GroupJournalEntries);

        var readOnlyDb = new ReadOnlyDebugDb(db);
        _playbackDb.SetSource(readOnlyDb);

        RetainForDispose(_openedDb);
        _openedDb = readOnlyDb;
        _openedSession = session;

        if (_openedTempDirectory != null && _openedTempDirectory != tempDir)
        {
            try { Directory.Delete(_openedTempDirectory, recursive: true); } catch { /* ignore */ }
        }
        _openedTempDirectory = tempDir;

        _sourceRevision++;
    }

    public void ExportSession(PlaybackSessionInfo session, string archivePath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(archivePath)!);

        if (session.IsCompacted && session.ArchivePath != null)
        {
            File.Copy(session.ArchivePath, archivePath, overwrite: true);
            return;
        }

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
        var fileName = Path.GetFileName(archivePath);
        var destinationPath = Path.Combine(_sessionRootDirectory, fileName);

        if (archivePath.EndsWith(".tyrlog", StringComparison.OrdinalIgnoreCase))
        {
            if (File.Exists(destinationPath))
            {
                Log.ZLogWarning($"Skipping session import from {archivePath} because {fileName} already exists.");
                return;
            }
            File.Copy(archivePath, destinationPath);
        }
        else
        {
            Directory.CreateDirectory(_sessionRootDirectory);
            using var archive = ZipFile.OpenRead(archivePath);
            var firstEntry = archive.Entries.FirstOrDefault(e => !string.IsNullOrEmpty(e.FullName) && !e.FullName.EndsWith('/'));
            if (firstEntry is null) return;

            var topLevelDirectory = firstEntry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(topLevelDirectory)) return;

            var destinationRoot = Path.Combine(_sessionRootDirectory, topLevelDirectory);
            if (Directory.Exists(destinationRoot)) return;

            archive.ExtractToDirectory(_sessionRootDirectory);
        }
        RefreshSessions();
    }

    public void RenameSession(PlaybackSessionInfo session, string? captureLabel)
    {
        lock (_lock)
        {
            DebugDbSessionMetadata? newMetadata = null;
            if (session.IsCompacted && session.ArchivePath != null)
            {
                using var archive = ZipFile.Open(session.ArchivePath, ZipArchiveMode.Update);
                var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("session.json"));
                if (entry != null)
                {
                    using (var stream = entry.Open())
                    {
                        newMetadata = System.Text.Json.JsonSerializer.Deserialize<DebugDbSessionMetadata>(stream) ?? throw new InvalidOperationException();
                    }
                    newMetadata.CaptureLabel = captureLabel;
                    entry.Delete();
                    var newEntry = archive.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    using (var stream = newEntry.Open())
                    {
                        System.Text.Json.JsonSerializer.Serialize(stream, newMetadata, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                    }
                }
            }
            else
            {
                newMetadata = DebugDbSessionMetadata.Load(session.MetadataPath);
                newMetadata.CaptureLabel = captureLabel;
                newMetadata.Save(session.MetadataPath);
            }

            if (newMetadata != null)
            {
                UpdateSessionInfoInPlace(session.MetadataPath, session with { Metadata = newMetadata });
                if (_openedSession?.MetadataPath == session.MetadataPath)
                    _openedSession = _openedSession with { Metadata = newMetadata };
            }
        }
    }

    public void AssignCaptureLabel(IEnumerable<PlaybackSessionInfo> sessions, string? captureLabel)
    {
        foreach (var session in sessions)
            RenameSession(session, captureLabel);
    }

    public void CompactSessions(IEnumerable<PlaybackSessionInfo> sessions)
    {
        lock (_lock)
        {
            foreach (var session in sessions)
            {
                if (session.IsCompacted) continue;
                if (string.Equals(Path.GetFullPath(session.DatabaseDirectory), Path.GetFullPath(_liveDatabaseDirectory), StringComparison.OrdinalIgnoreCase))
                    continue;

                var archivePath = Path.Combine(_sessionRootDirectory, session.Metadata.SessionId + ".tyrlog");
                if (File.Exists(archivePath)) continue;

                try
                {
                    ExportSession(session, archivePath);

                    if (_openedSession?.MetadataPath == session.MetadataPath)
                        SwitchToLive();

                    if (Directory.Exists(session.SessionDirectory))
                        Directory.Delete(session.SessionDirectory, recursive: true);

                    var compactedInfo = TryLoadCompactSession(archivePath);
                    if (compactedInfo != null)
                        UpdateSessionInfoInPlace(session.MetadataPath, compactedInfo);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Failed to compact session {session.Metadata.SessionId}: {ex.Message}");
                }
            }
        }
    }

    private void UpdateSessionInfoInPlace(string metadataPath, PlaybackSessionInfo newInfo)
    {
        var list = _sessions.ToList();
        for (var i = 0; i < list.Count; i++)
        {
            if (list[i].MetadataPath == metadataPath)
            {
                list[i] = newInfo;
                break;
            }
        }
        _sessions = list;
    }

    private void BackgroundLoop()
    {
        var ct = _cts.Token;
        int refreshCounter = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Discovery every 30s
                if (++refreshCounter >= 30)
                {
                    RefreshSessions();
                    refreshCounter = 0;
                }

                if (Tyr.Gui.Views.SessionsView.AutoCompact)
                {
                    List<PlaybackSessionInfo> toCompact;
                    lock (_lock)
                    {
                        var livePath = Path.GetFullPath(_liveDatabaseDirectory);
                        toCompact = _sessions.Where(s => !s.IsCompacted && !string.Equals(Path.GetFullPath(s.DatabaseDirectory), livePath, StringComparison.OrdinalIgnoreCase)).ToList();
                    }

                    if (toCompact.Count > 0)
                    {
                        Debug.WriteLine($"[AutoCompact] Compacting {toCompact.Count} sessions.");
                        CompactSessions(toCompact);
                    }
                }

                Thread.Sleep(TimeSpan.FromSeconds(1));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error in background loop: {ex}");
                Thread.Sleep(TimeSpan.FromSeconds(5));
            }
        }
    }

    public void DeleteSessions(IEnumerable<PlaybackSessionInfo> sessions)
    {
        lock (_lock)
        {
            var sessionsList = _sessions.ToList();
            foreach (var session in sessions)
            {
                if (_openedSession?.MetadataPath == session.MetadataPath)
                    SwitchToLive();

                if (session.IsCompacted && session.ArchivePath != null && File.Exists(session.ArchivePath))
                {
                    File.Delete(session.ArchivePath);
                }
                else if (Directory.Exists(session.SessionDirectory))
                {
                    Directory.Delete(session.SessionDirectory, recursive: true);
                }
                sessionsList.RemoveAll(s => s.MetadataPath == session.MetadataPath);
            }
            _sessions = sessionsList;
        }
    }

    public void RevealInExplorer(PlaybackSessionInfo session)
    {
        var path = session.IsCompacted ? session.ArchivePath : session.SessionDirectory;
        if (path == null) return;

        Process.Start(new ProcessStartInfo
        {
            FileName = "explorer.exe",
            Arguments = $"/select,\"{path}\"",
            UseShellExecute = true,
        });
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();

        _openedDb?.Dispose();
        foreach (var db in _retainedDbs)
            db.Dispose();

        if (Directory.Exists(_tempRoot))
        {
            try { Directory.Delete(_tempRoot, recursive: true); } catch { /* ignore */ }
        }
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

    private static PlaybackSessionInfo? TryLoadCompactSession(string archivePath)
    {
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            var entry = archive.Entries.FirstOrDefault(e => e.FullName.EndsWith("session.json"));
            if (entry == null) return null;

            using var stream = entry.Open();
            var metadata = System.Text.Json.JsonSerializer.Deserialize<DebugDbSessionMetadata>(stream);
            if (metadata == null) return null;

            var sessionId = metadata.SessionId;
            return new PlaybackSessionInfo(
                metadata,
                archivePath,
                sessionId,
                Path.Combine(sessionId, "db"),
                IsCompacted: true,
                ArchivePath: archivePath);
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
    string DatabaseDirectory,
    bool IsCompacted = false,
    string? ArchivePath = null)
{
    public string RangeLabel => FormatDuration(DurationSeconds);

    public double DurationSeconds
    {
        get
        {
            if (Metadata.FirstFrameTimestamp.HasValue && Metadata.LastFrameTimestamp.HasValue)
            {
                var frameSpan = Metadata.LastFrameTimestamp.Value - Metadata.FirstFrameTimestamp.Value;
                if (frameSpan > DeltaTime.Zero)
                    return frameSpan.Seconds;
            }

            if (Metadata.ClosedAtUtc.HasValue)
            {
                var wallClockSpan = Metadata.ClosedAtUtc.Value - Metadata.CreatedAtUtc;
                if (wallClockSpan > TimeSpan.Zero)
                    return wallClockSpan.TotalSeconds;
            }

            return 0.0;
        }
    }

    private static string FormatDuration(double seconds)
    {
        if (seconds <= 0) return "n/a";
        var t = TimeSpan.FromSeconds(seconds);
        if (t.TotalHours >= 1)
            return $"{(int)t.TotalHours}h {t.Minutes}m {t.Seconds}s";
        if (t.TotalMinutes >= 1)
            return $"{t.Minutes}m {t.Seconds}s";
        return $"{seconds:F2} s";
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
