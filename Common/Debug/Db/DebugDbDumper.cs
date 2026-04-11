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

    private readonly Subscriber<Debug.Logging.Entry> _logSubscriber = Hub.Logs.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Drawing.Command> _drawSubscriber = Hub.Draws.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Plotting.Command> _plotSubscriber = Hub.Plots.Subscribe(Mode.All);
    private readonly Subscriber<Debug.Frame> _frameSubscriber = Hub.Frames.Subscribe(Mode.All);
    private readonly Subscriber<Data.Ssl.Vision.Detection.Frame> _detectionSubscriber = Hub.RawDetection.Subscribe(Mode.All);
    private readonly Subscriber<Data.Ssl.Vision.Geometry.FieldSize> _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.All);
    private readonly Subscriber<Data.Ssl.Vision.Geometry.CameraCalibration> _calibrationSubscriber = Hub.CameraCalibration.Subscribe(Mode.All);
    private readonly Subscriber<Data.Ssl.Vision.Geometry.BallModels> _ballModelsSubscriber = Hub.BallModels.Subscribe(Mode.All);
    private readonly Subscriber<Vision.Data.FilteredFrame> _visionSubscriber = Hub.Vision.Subscribe(Mode.All);

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
            .RegisterType<Debug.Logging.Entry>()
            .RegisterType<Debug.Plotting.Command>()
            .RegisterType<Debug.Drawing.Command>()
            .RegisterType<Data.Ssl.Vision.Detection.Frame>()
            .RegisterType<Data.Ssl.Vision.Geometry.FieldSize>()
            .RegisterType<Data.Ssl.Vision.Geometry.CameraCalibration>()
            .RegisterType<Data.Ssl.Vision.Geometry.BallModels>()
            .RegisterType<Vision.Data.FilteredFrame>();

        _viewer = new DebugDbViewer(_db, ViewerPort)
            .Register<Debug.Logging.Entry>()
            .Register<Debug.Plotting.Command>()
            .Register<Debug.Drawing.Command>()
            .Register<Data.Ssl.Vision.Detection.Frame>()
            .Register<Data.Ssl.Vision.Geometry.FieldSize>()
            .Register<Data.Ssl.Vision.Geometry.CameraCalibration>()
            .Register<Data.Ssl.Vision.Geometry.BallModels>()
            .Register<Vision.Data.FilteredFrame>();

        Log.ZLogInformation($"DebugDb session started: {_session.SessionDirectory}");
        _viewer.Start();

        _runner = new RunnerSync(Tick, priority: RunnerPriority);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);
        _runner.Start();
    }

    private bool Tick()
    {
        var dumped = false;

        while (_frameSubscriber.Reader.TryRead(out var frame))
        {
            _db.AppendFrame(frame);
            _metadata.UpdateFrameRange(frame);
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

        while (_detectionSubscriber.Reader.TryRead(out var detection))
        {
            _db.Append(detection);
            dumped = true;
        }

        while (_fieldSizeSubscriber.Reader.TryRead(out var fieldSize))
        {
            _db.Append(fieldSize);
            dumped = true;
        }

        while (_calibrationSubscriber.Reader.TryRead(out var calibration))
        {
            _db.Append(calibration);
            dumped = true;
        }

        while (_ballModelsSubscriber.Reader.TryRead(out var ballModels))
        {
            _db.Append(ballModels);
            dumped = true;
        }

        while (_visionSubscriber.Reader.TryRead(out var vision))
        {
            _db.Append(vision);
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
        _detectionSubscriber.Dispose();
        _fieldSizeSubscriber.Dispose();
        _calibrationSubscriber.Dispose();
        _ballModelsSubscriber.Dispose();
        _visionSubscriber.Dispose();

        _runner.Stop();

        _viewer.Dispose();
        _db.Dispose();

        _metadata.ClosedAtUtc = DateTimeOffset.UtcNow;
        _metadata.Save(_session.MetadataPath);
    }
}
