using System.Collections.Concurrent;
using System.Linq.Expressions;
using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Drawing.Drawables;
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

    private readonly Subscriber<Debug.DebugDumpEntry> _dumpSubscriber = Debug.DebugBus.SubscribeDumpEntries(Mode.All);

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

        Log.ZLogInformation($"DebugDb session started: {_session.SessionDirectory}");
        _viewer.Start();

        _runner = new RunnerSync(Tick, priority: RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        var dumped = false;

        for (var entryIteration = 0; entryIteration < 1000; entryIteration++)
        {
            if (!_dumpSubscriber.Reader.TryRead(out var dumpEntry))
                break;

            if (dumpEntry.TryGetFrame(out var frame))
            {
                _db.AppendFrame(frame);
                _metadata.UpdateFrameRange(frame);
            }
            else if (dumpEntry.TryGetKnownEntry(out Debug.Logging.Entry logEntry))
            {
                _db.Append(logEntry);
            }
            else if (dumpEntry.TryGetEntry<Circle>(out var circle))
            {
                _db.Append(circle);
            }
            else if (dumpEntry.TryGetEntry<Arc>(out var arc))
            {
                _db.Append(arc);
            }
            else if (dumpEntry.TryGetEntry<Arrow>(out var arrow))
            {
                _db.Append(arrow);
            }
            else if (dumpEntry.TryGetEntry<Line>(out var line))
            {
                _db.Append(line);
            }
            else if (dumpEntry.TryGetEntry<LineSegment>(out var lineSegment))
            {
                _db.Append(lineSegment);
            }
            else if (dumpEntry.TryGetEntry<Drawing.Drawables.Path>(out var path))
            {
                _db.Append(path);
            }
            else if (dumpEntry.TryGetEntry<Point>(out var point))
            {
                _db.Append(point);
            }
            else if (dumpEntry.TryGetEntry<Rectangle>(out var rectangle))
            {
                _db.Append(rectangle);
            }
            else if (dumpEntry.TryGetEntry<Robot>(out var robot))
            {
                _db.Append(robot);
            }
            else if (dumpEntry.TryGetEntry<Text>(out var text))
            {
                _db.Append(text);
            }
            else if (dumpEntry.TryGetEntry<Triangle>(out var triangle))
            {
                _db.Append(triangle);
            }
            else if (dumpEntry.TryGetKnownEntry(out Debug.Plotting.Command plotCommand))
            {
                _db.Append(plotCommand);
            }
            else if (dumpEntry.TryGetCustomEntry(out var entry, out var entryType))
            {
                DebugEntryAppender.Append(_db, entryType, entry);
            }

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
        _dumpSubscriber.Dispose();

        _runner.Stop();

        _viewer.Dispose();
        _db.Dispose();

        _metadata.ClosedAtUtc = DateTimeOffset.UtcNow;
        _metadata.Save(_session.MetadataPath);
    }

    private static class DebugEntryAppender
    {
        private static readonly ConcurrentDictionary<Type, Action<DebugDb, object>> AppendActions = new();

        public static void Append(DebugDb db, Type type, object entry)
        {
            var append = AppendActions.GetOrAdd(type, CreateAppendAction);
            append(db, entry);
        }

        private static Action<DebugDb, object> CreateAppendAction(Type type)
        {
            var appendMethod = typeof(DebugDb).GetMethod(nameof(DebugDb.Append))!
                .MakeGenericMethod(type);

            var db = Expression.Parameter(typeof(DebugDb), "db");
            var entry = Expression.Parameter(typeof(object), "entry");
            var body = Expression.Call(db, appendMethod, Expression.Convert(entry, type));

            return Expression.Lambda<Action<DebugDb, object>>(body, db, entry).Compile();
        }
    }
}
