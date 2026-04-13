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
    private readonly List<Debug.Frame> _frameBuffer = new();
    private readonly IReadOnlyList<IDebugEntrySubscription> _entrySubscriptions;

    private readonly RunnerSync _runner;
    private readonly DebugDb _db;
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

        _viewer = new DebugDbViewer(_db, ViewerPort)
            .RegisterAllRegisteredTypes();

        _entrySubscriptions = DebugTypeRegistry.GetRegisteredTypes()
            .Select(DebugEntrySubscription.Create)
            .ToArray();

        Log.ZLogInformation($"DebugDb session started: {_session.SessionDirectory}");
        _viewer.Start();

        _runner = new RunnerSync(Tick, tickRateHz: 100, priority: RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        var hasData = false;

        foreach (var subscription in _entrySubscriptions)
        {
            subscription.Buffer();
            if (subscription.HasData) hasData = true;
        }

        while (_frameSubscriber.Reader.TryRead(out var frame))
        {
            _frameBuffer.Add(frame);
            hasData = true;
        }

        if (!hasData)
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        _db.RunBatch(() =>
        {
            // Data must be written before the frame boundary that marks it as "completed".
            // RunnerSync publishes draw/log/plot commands first, then the frame boundary.
            // Processing frames first would make frames appear completed before their data
            // is in the DB, causing the GUI to display an empty frame during that window.
            foreach (var subscription in _entrySubscriptions)
                subscription.Flush(_db);

            foreach (var frame in _frameBuffer)
            {
                _db.AppendFrame(frame);
                _metadata.UpdateFrameRange(frame);
            }
        });

        foreach (var subscription in _entrySubscriptions)
            subscription.Clear();
        _frameBuffer.Clear();
        
        Plot.Plot("dumper FPS", _runner.Timer.Fps);
        
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
        void Buffer();
        void Flush(IDebugDb db);
        void Clear();
        bool HasData { get; }
    }

    private sealed class DebugEntrySubscription<T> : IDebugEntrySubscription where T : struct, Debug.IEntry
    {
        private readonly Subscriber<T> _subscriber = Debug.DebugBus.Subscribe<T>(Mode.All);
        private readonly List<T> _buffer = new();

        public bool HasData => _buffer.Count > 0;

        public void Buffer()
        {
            while (_subscriber.Reader.TryRead(out var entry))
                _buffer.Add(entry);
        }

        public void Flush(IDebugDb db)
        {
            foreach (var entry in _buffer)
                db.Append(entry);
        }

        public void Clear()
        {
            _buffer.Clear();
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
