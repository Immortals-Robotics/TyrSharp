using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Extensions;
using Tyr.Common.Runner;
using Tyr.Common.Time;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

[Configurable]
public sealed partial class TeamRunner : IDisposable
{
    [ConfigEntry] private static DeltaTime SleepTime { get; set; } = DeltaTime.FromMilliseconds(1);
    [ConfigEntry] private static ThreadPriority RunnerPriority { get; set; } = ThreadPriority.Highest;

    private readonly Subscriber<Referee.State> _refereeSubscriber;
    private readonly Subscriber<Vision.FilteredFrame> _visionSubscriber;
    private readonly Subscriber<FieldSize> _fieldSizeSubscriber;
    private readonly Subscriber<Common.Data.Robot.StatusUpdate> _robotStatusSubscriber;

    private readonly RunnerSync _runner;

    private Referee.State _referee = new();
    private FieldSize _field = FieldSize.DivisionA;

    private readonly TeamColor _color;
    private readonly Ai _ai;
    private readonly ManualControlState _manualControl;

    private readonly Knowledge.Knowledge _knowledge = new();

    private static TeamRunner? _yellowRunner;
    private static TeamRunner? _blueRunner;

    public TeamRunner(TeamColor color)
    {
        _color = color;

        _refereeSubscriber = Hub.Referee.Subscribe(Mode.Latest);
        _visionSubscriber = Hub.Vision.Subscribe(Mode.Latest);
        _fieldSizeSubscriber = Hub.FieldSize.Subscribe(Mode.Latest);
        _robotStatusSubscriber = Hub.RobotStatus.Subscribe(Mode.All);

        _runner = new RunnerSync(Tick, 0, $"{ModuleName}{color}", RunnerPriority);
        _runner.SetInit(Init);
        Configurable.OnUpdated += _ => _runner.SetPriority(RunnerPriority);

        _manualControl = new ManualControlState(color);
        _ai = new Ai(_manualControl);

        if (color == TeamColor.Yellow) _yellowRunner = this;
        else if (color == TeamColor.Blue) _blueRunner = this;
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
            Plot.Plot($"Robot {status.Info?.RobotId} accelerometer", new Vector3(status.Imu?.AccelX ?? 0f, status.Imu?.AccelY ?? 0f, status.Imu?.AccelZ ?? 0f));

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
                $"ball={status.IrSensor?.Blocked} " +
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
        if (update.RobotId < 0 || update.RobotId >= Context.OwnRobots.Count) return;

        Context.OwnRobots[update.RobotId].HardwareStatus.Apply(update);
    }

    public void Dispose()
    {
        _refereeSubscriber.Dispose();
        _visionSubscriber.Dispose();
        _fieldSizeSubscriber.Dispose();
        _robotStatusSubscriber.Dispose();

        _runner.Stop();
    }

    public static bool IsRunning(TeamColor color) => GetRunner(color)?._runner.IsRunning ?? false;

    public static ManualControlSnapshot GetManualControlSnapshot(TeamColor color) =>
        GetRunner(color)?._manualControl.GetSnapshot() ?? new ManualControlSnapshot(
            color,
            false,
            false,
            0,
            ManualSkillAction.GoToPoint,
            ManualShotMode.Kick,
            6f,
            4f,
            ManualShotTargetMode.TargetPoint,
            false,
            Vector2.Zero,
            GoToPointFacingMode.Angle,
            90f,
            false,
            false,
            Vector2.Zero);

    public static void SetManualEnabled(TeamColor color, bool enabled)
    {
        GetRunner(color)?._manualControl.SetEnabled(enabled);
    }

    public static void SetManualRunning(TeamColor color, bool running)
    {
        GetRunner(color)?._manualControl.SetRunning(running);
    }

    public static void SetManualSelectedRobot(TeamColor color, int robotId)
    {
        GetRunner(color)?._manualControl.SetSelectedRobot(robotId);
    }

    public static void SetManualAction(TeamColor color, ManualSkillAction action)
    {
        GetRunner(color)?._manualControl.SetAction(action);
    }

    public static void SetManualShotMode(TeamColor color, ManualShotMode shotMode)
    {
        GetRunner(color)?._manualControl.SetShotMode(shotMode);
    }

    public static void SetManualKickSpeedMps(TeamColor color, float kickSpeedMps)
    {
        GetRunner(color)?._manualControl.SetKickSpeedMps(kickSpeedMps);
    }

    public static void SetManualChipDistanceMeters(TeamColor color, float chipDistanceMeters)
    {
        GetRunner(color)?._manualControl.SetChipDistanceMeters(chipDistanceMeters);
    }

    public static void SetManualShotTargetMode(TeamColor color, ManualShotTargetMode shotTargetMode)
    {
        GetRunner(color)?._manualControl.SetShotTargetMode(shotTargetMode);
    }

    public static void SetManualTargetPoint(TeamColor color, Vector2 point)
    {
        GetRunner(color)?._manualControl.SetTargetPoint(point);
    }

    public static void ClearManualTargetPoint(TeamColor color)
    {
        GetRunner(color)?._manualControl.ClearTargetPoint();
    }

    public static void SetManualAwaitingPoint(TeamColor color, bool awaitingPoint)
    {
        GetRunner(color)?._manualControl.SetAwaitingLookTarget(awaitingPoint);
    }

    public static void SetGoToPointFacingMode(TeamColor color, GoToPointFacingMode mode)
    {
        GetRunner(color)?._manualControl.SetGoToPointFacingMode(mode);
    }

    public static void SetGoToPointFacingAngle(TeamColor color, float angleDeg)
    {
        GetRunner(color)?._manualControl.SetGoToPointFacingAngle(angleDeg);
    }

    public static void SetManualLookTarget(TeamColor color, Vector2 point)
    {
        GetRunner(color)?._manualControl.SetLookTarget(point);
    }

    public static void ClearManualLookTarget(TeamColor color)
    {
        GetRunner(color)?._manualControl.ClearLookTarget();
    }

    private static TeamRunner? GetRunner(TeamColor color)
    {
        return color switch
        {
            TeamColor.Yellow => _yellowRunner,
            TeamColor.Blue => _blueRunner,
            _ => null
        };
    }
}
