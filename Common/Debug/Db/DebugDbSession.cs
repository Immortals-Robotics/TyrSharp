using Tyr.Common.Config;

namespace Tyr.Common.Debug.Db;

[Configurable]
public sealed partial class DebugDbSession : IDisposable
{
    [ConfigEntry(StorageType.User)] private static string RootDirectory { get; set; } = "";
    [ConfigEntry(StorageType.User)] private static string CaptureLabel { get; set; } = "";
    [ConfigEntry] private static int ViewerPort { get; set; } = 9000;

    private readonly DebugDb _db;
    private readonly DebugDbViewer _viewer;
    private readonly DebugDbSessionDescriptor _session;
    private readonly DebugDbSessionMetadata _metadata;

    public DebugDb Db => _db;
    public string SessionRootDirectory => _session.RootDirectory;
    public string SessionDirectory => _session.SessionDirectory;
    public string DatabaseDirectory => _session.DatabaseDirectory;

    public DebugDbSession()
    {
        _session = DebugDbSessionPaths.CreateSession(RootDirectory, CaptureLabel);
        _metadata = DebugDbSessionMetadata.Create(_session);
        _metadata.Save(_session.MetadataPath);

        _db = new DebugDb(_session.DatabaseDirectory)
            .RegisterKnownTypes();
        DebugBus.SetDb(_db);

        _viewer = new DebugDbViewer(_db, ViewerPort)
            .RegisterAllRegisteredTypes();
        Log.ZLogInformation($"DebugDb session started: {_session.SessionDirectory}");
        _viewer.Start();
    }

    public void Dispose()
    {
        DebugBus.SetDb(null);

        var frameRange = _db.GetFrameRange();
        _metadata.FirstFrameTimestamp = frameRange?.Start;
        _metadata.LastFrameTimestamp = frameRange?.End;
        _metadata.ClosedAtUtc = DateTimeOffset.UtcNow;
        _metadata.Save(_session.MetadataPath);

        _viewer.Dispose();
        _db.Dispose();
    }
}
