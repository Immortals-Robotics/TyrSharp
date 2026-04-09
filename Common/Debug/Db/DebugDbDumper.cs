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
    [ConfigEntry] private static string Directory { get; set; } = "debug_session";
    [ConfigEntry] private static int ViewerPort { get; set; } = 9000;

    private readonly Subscriber<Debug.Logging.Entry> _logSubscriber = Hub.Logs.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Drawing.Command> _drawSubscriber = Hub.Draws.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Plotting.Command> _plotSubscriber = Hub.Plots.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Frame> _frameSubscriber = Hub.Frames.Subscribe(Mode.All);

    private readonly RunnerSync _runner;
    private readonly DebugDb _db;
    private readonly DebugDbViewer _viewer;

    public IDebugDb Db => _db;

    public DebugDbDumper()
    {
        _db = new DebugDb(Directory)
            .RegisterType<Debug.Logging.Entry>()
            .RegisterType<Debug.Plotting.Command>()
            .RegisterType<Debug.Drawing.Command>();

        _viewer = new DebugDbViewer(_db, ViewerPort)
            .Register<Debug.Logging.Entry>()
            .Register<Debug.Plotting.Command>()
            .Register<Debug.Drawing.Command>();

        _viewer.Start();

        _runner = new RunnerSync(Tick);
        _runner.Start();
    }

    private bool Tick()
    {
        var dumped = false;

        while (_frameSubscriber.Reader.TryRead(out var frame))
        {
            _db.AppendFrame(frame);
            dumped = true;
        }

        while (_logSubscriber.Reader.TryRead(out var log))
        {
            _db.Append(log);
            dumped = true;
        }

        while (_drawSubscriber.Reader.TryRead(out var draw))
        {
            _db.Append(draw);
            dumped = true;
        }

        while (_plotSubscriber.Reader.TryRead(out var plot))
        {
            _db.Append(plot);
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
        _logSubscriber.Dispose();
        _drawSubscriber.Dispose();
        _plotSubscriber.Dispose();
        _frameSubscriber.Dispose();

        _runner.Stop();

        _viewer.Dispose();
        _db.Dispose();
    }
}
