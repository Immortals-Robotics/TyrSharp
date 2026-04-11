using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Runner;
using Tyr.Common.Time;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

public enum ManualSkillAction
{
    GoToPoint,
    KickBall,
    WaitForBall,
    InterceptBall,
    OneTouch,
    TurnAndShoot,
    DribbleToDirection,
}

public enum GoToPointFacingMode
{
    Angle,
    LookAtTarget,
}

public readonly record struct ManualControlSnapshot(
    TeamColor Team,
    bool Enabled,
    bool Running,
    int SelectedRobotId,
    ManualSkillAction Action,
    bool HasTargetPoint,
    Vector2 TargetPoint,
    GoToPointFacingMode GoToPointFacingMode,
    float GoToPointFacingAngleDeg,
    bool AwaitingLookTarget,
    bool HasLookTarget,
    Vector2 LookTarget);

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

internal sealed class ManualControlState(TeamColor team)
{
    private readonly object _gate = new();

    private bool _enabled;
    private bool _running;
    private int _selectedRobotId;
    private ManualSkillAction _action = ManualSkillAction.GoToPoint;
    private bool _hasTargetPoint;
    private Vector2 _targetPoint;
    private GoToPointFacingMode _goToPointFacingMode;
    private float _goToPointFacingAngleDeg = 90f;
    private bool _awaitingLookTarget;
    private bool _hasLookTarget;
    private Vector2 _lookTarget;

    private readonly KickBall _kickBall = new();
    private readonly WaitForBall _waitForBall = new();
    private readonly InterceptBall _interceptBall = new();
    private readonly OneTouch _oneTouch = new();
    private readonly TurnAndShoot _turnAndShoot = new();
    private readonly DribbleToDirection _dribbleToDirection = new();

    public ManualControlSnapshot GetSnapshot()
    {
        lock (_gate)
        {
            return new ManualControlSnapshot(
                team,
                _enabled,
                _running,
                _selectedRobotId,
                _action,
                _hasTargetPoint,
                _targetPoint,
                _goToPointFacingMode,
                _goToPointFacingAngleDeg,
                _awaitingLookTarget,
                _hasLookTarget,
                _lookTarget);
        }
    }

    public void SetEnabled(bool enabled)
    {
        lock (_gate)
        {
            _enabled = enabled;
            if (!enabled)
            {
                _running = false;
                _awaitingLookTarget = false;
            }
        }
    }

    public void SetRunning(bool running)
    {
        lock (_gate)
        {
            _running = _enabled && running;
        }
    }

    public void SetSelectedRobot(int robotId)
    {
        lock (_gate)
        {
            _selectedRobotId = Math.Clamp(robotId, 0, CommonConfigs.MaxRobots - 1);
        }
    }

    public void SetAction(ManualSkillAction action)
    {
        lock (_gate)
        {
            _action = action;
        }
    }

    public void SetTargetPoint(Vector2 point)
    {
        lock (_gate)
        {
            _targetPoint = point;
            _hasTargetPoint = true;
        }
    }

    public void ClearTargetPoint()
    {
        lock (_gate)
        {
            _hasTargetPoint = false;
        }
    }

    public void SetAwaitingLookTarget(bool awaitingLookTarget)
    {
        lock (_gate)
        {
            _awaitingLookTarget = _enabled && awaitingLookTarget;
        }
    }

    public void SetGoToPointFacingMode(GoToPointFacingMode mode)
    {
        lock (_gate)
        {
            _goToPointFacingMode = mode;
        }
    }

    public void SetGoToPointFacingAngle(float angleDeg)
    {
        lock (_gate)
        {
            _goToPointFacingAngleDeg = angleDeg;
        }
    }

    public void SetLookTarget(Vector2 point)
    {
        lock (_gate)
        {
            _lookTarget = point;
            _hasLookTarget = true;
            _awaitingLookTarget = false;
        }
    }

    public void ClearLookTarget()
    {
        lock (_gate)
        {
            _hasLookTarget = false;
            _awaitingLookTarget = false;
        }
    }

    public bool TryExecute()
    {
        lock (_gate)
        {
            DrawOverlay();

            if (!_enabled)
            {
                return false;
            }

            var robots = Context.OwnRobots;
            for (var i = 0; i < robots.Count; i++)
            {
                if (i == _selectedRobotId) continue;
                if (!robots[i].Seen) continue;
                robots[i].Halt();
            }

            if (_selectedRobotId < 0 || _selectedRobotId >= robots.Count)
            {
                return true;
            }

            var robot = robots[_selectedRobotId];
            if (!robot.Seen)
            {
                return true;
            }

            if (!_running)
            {
                robot.Halt();
                return true;
            }

            ExecuteSelected(robot);
            return true;
        }
    }

    private void DrawOverlay()
    {
        if (!_enabled)
        {
            return;
        }

        var color = team.ToColor();

        if (_hasTargetPoint)
        {
            Draw.DrawCircle(_targetPoint, 90f, color, options: Options.Outline(25f));
            Draw.DrawCircle(_targetPoint, 25f, color, options: Options.Filled);
        }

        if (_hasLookTarget)
        {
            Draw.DrawCircle(_lookTarget, 70f, color, options: Options.Outline(20f));
        }

        if (_selectedRobotId < 0 || _selectedRobotId >= Context.OwnRobots.Count)
        {
            return;
        }

        var robot = Context.OwnRobots[_selectedRobotId];
        if (!robot.Seen)
        {
            return;
        }

        Draw.DrawCircle(robot.Position, 170f, color, options: Options.Outline(25f));
    }

    private void ExecuteSelected(Robot.Robot robot)
    {
        switch (_action)
        {
            case ManualSkillAction.GoToPoint:
                if (!_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                if (_goToPointFacingMode == GoToPointFacingMode.LookAtTarget && _hasLookTarget)
                {
                    robot.Face(_lookTarget);
                }
                else
                {
                    robot.TargetAngle = Angle.FromDeg(_goToPointFacingAngleDeg);
                }

                robot.Navigate(_targetPoint, VelocityProfile.Mamooli);
                return;

            case ManualSkillAction.KickBall:
                if (!_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _kickBall.Angle = Context.Ball.State.Position.AngleWith(_targetPoint);
                _kickBall.Kick = 6f;
                _kickBall.Chip = 0f;
                _kickBall.IsGoalkeeper = false;
                _kickBall.Execute(robot);
                return;

            case ManualSkillAction.WaitForBall:
                if (!_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _waitForBall.StaticPosition = _targetPoint;
                _waitForBall.Execute(robot);
                return;

            case ManualSkillAction.InterceptBall:
                _interceptBall.Angle = Context.Ball.State.Velocity.Xy().ToAngle();
                _interceptBall.WaitTimeSeconds = 0f;
                _interceptBall.Execute(robot);
                return;

            case ManualSkillAction.OneTouch:
                _oneTouch.TargetPoint = _hasTargetPoint ? _targetPoint : null;
                _oneTouch.Kick = 6f;
                _oneTouch.Chip = 0f;
                _oneTouch.Execute(robot);
                return;

            case ManualSkillAction.TurnAndShoot:
                if (!_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _turnAndShoot.Angle = Context.Ball.State.Position.AngleWith(_targetPoint);
                _turnAndShoot.Kick = 6f;
                _turnAndShoot.Chip = 0f;
                _turnAndShoot.Execute(robot);
                return;

            case ManualSkillAction.DribbleToDirection:
                if (!_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _dribbleToDirection.Direction = Context.Ball.State.Position.AngleWith(_targetPoint);
                _dribbleToDirection.Execute(robot);
                return;

            default:
                robot.Halt();
                return;
        }
    }
}
