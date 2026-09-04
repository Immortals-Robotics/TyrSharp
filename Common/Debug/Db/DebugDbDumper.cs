using Tyr.Common.Config;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Debug.Db;

[Configurable]
public sealed partial class DebugDbDumper : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);
    [ConfigEntry(StorageType.User)] private static string RootDirectory { get; set; } = "";
    [ConfigEntry(StorageType.User)] private static string CaptureLabel { get; set; } = "";
    [ConfigEntry] private static int ViewerPort { get; set; } = 9000;
    [ConfigEntry] private static ThreadPriority RunnerPriority { get; set; } = ThreadPriority.BelowNormal;

    private readonly RunnerSync _runner;
    private readonly DebugDb _db;
    private readonly DebugDbIngest _ingest;
    private readonly DebugDbViewer _viewer;
    private readonly DebugDbSessionDescriptor _session;
    private readonly DebugDbSessionMetadata _metadata;

    public IDebugDb Db => _db;
    public string SessionRootDirectory => _session.RootDirectory;
    public string SessionDirectory => _session.SessionDirectory;
    public string DatabaseDirectory => _session.DatabaseDirectory;

    public DebugDbDumper()
    {
        _session = DebugDbSessionPaths.CreateSession(RootDirectory, CaptureLabel);
        _metadata = DebugDbSessionMetadata.Create(_session);
        _metadata.Save(_session.MetadataPath);

        _db = new DebugDb(_session.DatabaseDirectory)
            .RegisterKnownTypes();

        // Subscribes to every registered entry type, now and as more get registered.
        _ingest = new DebugDbIngest(_db);
        _ingest.FrameIngested += _metadata.UpdateFrameRange;

        _viewer = new DebugDbViewer(_db, ViewerPort)
            .RegisterAllRegisteredTypes();

        Log.ZLogInformation($"DebugDb session started: {_session.SessionDirectory}");
        _viewer.Start();

        _runner = new RunnerSync(Tick, priority: RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        if (_ingest.Pump())
            return true;

        Thread.Sleep(SleepTime.ToTimeSpan());
        return false;
    }

    public void Dispose()
    {
        _runner.Stop();

        _ingest.Dispose();
        _viewer.Dispose();
        _db.Dispose();

        _metadata.ClosedAtUtc = DateTimeOffset.UtcNow;
        _metadata.Save(_session.MetadataPath);
    }
}
