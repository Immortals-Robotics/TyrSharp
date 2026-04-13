using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;
using Debug = Tyr.Common.Debug;

namespace Tyr.Common.Debug.Db;

[Configurable]
public sealed partial class DebugDbDumper : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);
    [ConfigEntry(StorageType.User)] private static string RootDirectory { get; set; } = "";
    [ConfigEntry(StorageType.User)] private static string CaptureLabel { get; set; } = "";
    [ConfigEntry] private static int ViewerPort { get; set; } = 9000;
    [ConfigEntry] private static ThreadPriority RunnerPriority { get; set; } = ThreadPriority.BelowNormal;

    private readonly Subscriber<Debug.Frame> _frameSubscriber = Hub.Frames.Subscribe(Mode.All);
    private readonly IReadOnlyList<IDebugEntrySubscription> _entrySubscriptions;

    private readonly RunnerSync _runner;
    private readonly DebugDb _db;
    private readonly DebugDbViewer _viewer;
    private readonly DebugDbSessionDescriptor _session;
    private readonly DebugDbSessionMetadata _metadata;

    public DebugDb Db => _db;
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

        _viewer = new DebugDbViewer(_db, ViewerPort)
            .RegisterAllRegisteredTypes();

        _entrySubscriptions = DebugTypeRegistry.GetRegisteredTypes()
            .Select(DebugEntrySubscription.Create)
            .ToArray();

        Log.ZLogInformation($"DebugDb session started: {_session.SessionDirectory}");
        _viewer.Start();

        _runner = new RunnerSync(Tick, priority: RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        var dumped = false;

        // Data must be written before the frame boundary that marks it as "completed".
        // RunnerSync publishes draw/log/plot commands first, then the frame boundary.
        // Processing frames first would make frames appear completed before their data
        // is in the DB, causing the GUI to display an empty frame during that window.
        foreach (var subscription in _entrySubscriptions)
            dumped |= subscription.Drain(_db);

        while (_frameSubscriber.Reader.TryRead(out var frame))
        {
            _db.AppendFrame(frame);
            _metadata.UpdateFrameRange(frame);
            dumped = true;
        }

        if (!dumped)
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        return true;
    }

    public void Dispose()
    {
        foreach (var subscription in _entrySubscriptions)
            subscription.Dispose();
        _frameSubscriber.Dispose();

        _runner.Stop();

        _viewer.Dispose();
        _db.Dispose();

        _metadata.ClosedAtUtc = DateTimeOffset.UtcNow;
        _metadata.Save(_session.MetadataPath);
    }

    private interface IDebugEntrySubscription : IDisposable
    {
        bool Drain(DebugDb db);
    }

    private sealed class DebugEntrySubscription<T> : IDebugEntrySubscription where T : struct, Debug.IEntry
    {
        private readonly Subscriber<T> _subscriber = Debug.DebugBus.Subscribe<T>(Mode.All);

        public bool Drain(DebugDb db)
        {
            var dumped = false;

            while (_subscriber.Reader.TryRead(out var entry))
            {
                db.Append(entry);
                dumped = true;
            }

            return dumped;
        }

        public void Dispose()
        {
            _subscriber.Dispose();
        }
    }

    private static class DebugEntrySubscription
    {
        public static IDebugEntrySubscription Create(Type type)
        {
            var subscriptionType = typeof(DebugEntrySubscription<>).MakeGenericType(type);
            return (IDebugEntrySubscription)Activator.CreateInstance(subscriptionType)!;
        }
    }
}
