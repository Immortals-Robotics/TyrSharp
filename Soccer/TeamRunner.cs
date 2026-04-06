using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

[Configurable]
public sealed partial class TeamRunner : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);

    private readonly Subscriber<Referee.State> _refereeSubscriber;
    private readonly Subscriber<Vision.FilteredFrame> _visionSubscriber;
    private readonly Subscriber<FieldSize> _fieldSizeSubscriber;
    private readonly Subscriber<Common.Data.Robot.StatusUpdate> _robotStatusSubscriber;

    private readonly RunnerSync _runner;

    private Referee.State _referee = new();
    private FieldSize _field = FieldSize.DivisionA;

    private readonly TeamColor _color;
    private readonly Ai _ai;

    private readonly Knowledge.Knowledge _knowledge = new();

    // Robot ID learned from the first RobotInfo frame; -1 until known
    private int _hwRobotId = -1;

    public TeamRunner(TeamColor color)
    {
        _color = color;

        _refereeSubscriber = Hub.Referee.Subscribe(Mode.Latest);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.Latest);
        _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
        _robotStatusSubscriber = Hub.RobotStatus.Subscribe(Mode.All);

        _runner = new RunnerSync(Tick, 0, $"{ModuleName}{color}");
        _runner.SetInit(Init);

        _ai = new Ai();
    }

    public void Start() => _runner.Start();
    public void Stop() => _runner.Stop();

    private bool Init()
    {
        // set a default empty context
        Context.Data.Value = new ContextData()
        {
            Color = _color,
            Ball = default,
            OppRobots = [],
            OwnRobots = [],
            Referee = _referee,
            Field = _field,
            Timer = _runner.Timer,
            Knowledge = _knowledge,
        };

        _ai.Init();

        return true;
    }

    private bool Tick()
    {
        if (!_visionSubscriber.Reader.TryRead(out var vision))
        {
            Thread.Sleep(SleepTime.ToTimeSpan());
            return false;
        }

        while (_robotStatusSubscriber.Reader.TryRead(out var statusUpdate))
            ApplyRobotStatus(statusUpdate);

        foreach (var robot in Context.OwnRobots.Where(r => r.HardwareStatus.Info != null))
        {
            var status = robot.HardwareStatus;

            Plot.Plot($"Robot {status.Info?.RobotId} battery", status.Power?.V24Voltage ?? 0f);
            Plot.Plot($"Robot {status.Info?.RobotId} gyro", new Vector3(status.Imu?.GyroX ?? 0f, status.Imu?.GyroY ?? 0f, status.Imu?.GyroZ ?? 0f));
            Plot.Plot($"Robot {status.Info?.RobotId} accelometer", new Vector3(status.Imu?.AccelX ?? 0f, status.Imu?.AccelY ?? 0f, status.Imu?.AccelZ ?? 0f));

            if (status.Motors?.Motors != null)
            {
                for (var i = 0; i < status.Motors.Motors.Count; i++)
                {
                    Plot.Plot($"Robot {status.Info?.RobotId} motor {i}",
                       new Vector2(status.Motors.Motors[i].Actual, status.Motors.Motors[i].Target));
                }
            }

            Log.ZLogDebug(
                $"Robot {status.Info!.RobotId}: " +
                $"battery={status.Power?.V24Voltage:F2}V " +
                $"temp={status.Diag?.ImuTemp:F1}°C " +
                $"ball={status.Info.BallDetected} " +
                $"motors=[{string.Join(", ", status.Motors?.Motors.Select(m => $"{m.Target:F0}/{m.Actual:F0}") ?? [])}]");
        }

        if (_refereeSubscriber.Reader.TryRead(out var referee))
            _referee = referee;

        if (_fieldSizeSubscriber.Reader.TryRead(out var field))
            _field = field;

        _ai.UpdateContext(vision, _referee, _field);

        _ai.Process();

        _ai.PublishCommands();

        return true;
    }

    private void ApplyRobotStatus(Common.Data.Robot.StatusUpdate update)
    {
        // RobotInfo is the only frame that carries the robot ID — learn it first
        if (update.Info is { } info)
            _hwRobotId = (int)info.RobotId;

        if (_hwRobotId < 0 || _hwRobotId >= Context.OwnRobots.Count) return;

        Context.OwnRobots[_hwRobotId].HardwareStatus.Apply(update);
    }

    public void Dispose()
    {
        _refereeSubscriber.Dispose();
        _visionSubscriber.Dispose();
        _fieldSizeSubscriber.Dispose();
        _robotStatusSubscriber.Dispose();

        _runner.Stop();
    }
}
