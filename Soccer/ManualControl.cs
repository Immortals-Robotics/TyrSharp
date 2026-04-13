using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Soccer.Helpers;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer;

public enum ManualSkillAction
{
    GoToPoint,
    KickBall,
    WaitForBall,
    InterceptBall,
    InterceptV2,
    CaptureBall,
    OneTouch,
    TurnAndShoot,
    DribbleToDirection,
}

public enum GoToPointFacingMode
{
    Angle,
    LookAtTarget,
}

public enum ManualShotMode
{
    Kick,
    Chip,
}

public enum ManualShotTargetMode
{
    TargetPoint,
    OpenAngle,
}

public readonly record struct ManualControlSnapshot(
    TeamColor Team,
    bool Enabled,
    bool Running,
    int SelectedRobotId,
    ManualSkillAction Action,
    ManualShotMode ShotMode,
    float KickSpeedMps,
    float ChipDistanceMeters,
    ManualShotTargetMode ShotTargetMode,
    bool HasTargetPoint,
    Vector2 TargetPoint,
    GoToPointFacingMode GoToPointFacingMode,
    float GoToPointFacingAngleDeg,
    bool AwaitingLookTarget,
    bool HasLookTarget,
    Vector2 LookTarget);

internal sealed class ManualControlState(TeamColor team)
{
    private readonly object _gate = new();

    private bool _enabled;
    private bool _running;
    private int _selectedRobotId;
    private ManualSkillAction _action = ManualSkillAction.GoToPoint;
    private ManualShotMode _shotMode = ManualShotMode.Kick;
    private float _kickSpeedMps = 6f;
    private float _chipDistanceMeters = 4f;
    private ManualShotTargetMode _shotTargetMode;
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
    private readonly InterceptV2 _interceptV2 = new();
    private readonly CaptureBall _captureBall = new();
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
                _shotMode,
                _kickSpeedMps,
                _chipDistanceMeters,
                _shotTargetMode,
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

    public void SetShotMode(ManualShotMode shotMode)
    {
        lock (_gate)
        {
            _shotMode = shotMode;
        }
    }

    public void SetKickSpeedMps(float kickSpeedMps)
    {
        lock (_gate)
        {
            _kickSpeedMps = Math.Max(0f, kickSpeedMps);
        }
    }

    public void SetChipDistanceMeters(float chipDistanceMeters)
    {
        lock (_gate)
        {
            _chipDistanceMeters = Math.Max(0f, chipDistanceMeters);
        }
    }

    public void SetShotTargetMode(ManualShotTargetMode shotTargetMode)
    {
        lock (_gate)
        {
            _shotTargetMode = shotTargetMode;
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
                if (i == _selectedRobotId || !robots[i].Seen)
                {
                    continue;
                }

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

            robot.IgnoreRefereeBallObstacle = RequiresBallAccess(_action);
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
                if (RequiresTargetPoint() && !_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _kickBall.Angle = ResolveBallSkillAngle(robot);
                ApplyShot(out var kickBallKick, out var kickBallChip);
                _kickBall.Kick = kickBallKick;
                _kickBall.Chip = kickBallChip;
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

            case ManualSkillAction.InterceptV2:
                _interceptV2.Execute(robot);
                return;

            case ManualSkillAction.CaptureBall:
                _captureBall.Execute(robot);
                return;

            case ManualSkillAction.OneTouch:
                if (RequiresTargetPoint() && !_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _oneTouch.TargetPoint = _shotTargetMode == ManualShotTargetMode.OpenAngle ? null : _targetPoint;
                ApplyShot(out var oneTouchKick, out var oneTouchChip);
                _oneTouch.Kick = oneTouchKick;
                _oneTouch.Chip = oneTouchChip;
                _oneTouch.Execute(robot);
                return;

            case ManualSkillAction.TurnAndShoot:
                if (RequiresTargetPoint() && !_hasTargetPoint)
                {
                    robot.Halt();
                    return;
                }

                _turnAndShoot.Angle = ResolveBallSkillAngle(robot);
                ApplyShot(out var turnAndShootKick, out var turnAndShootChip);
                _turnAndShoot.Kick = turnAndShootKick;
                _turnAndShoot.Chip = turnAndShootChip;
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

    private static bool RequiresBallAccess(ManualSkillAction action)
    {
        return action is
            ManualSkillAction.KickBall or
            ManualSkillAction.WaitForBall or
            ManualSkillAction.InterceptBall or
            ManualSkillAction.InterceptV2 or
            ManualSkillAction.CaptureBall or
            ManualSkillAction.OneTouch or
            ManualSkillAction.TurnAndShoot or
            ManualSkillAction.DribbleToDirection;
    }

    private bool RequiresTargetPoint()
    {
        return _action switch
        {
            ManualSkillAction.KickBall or
                ManualSkillAction.OneTouch or
                ManualSkillAction.TurnAndShoot => _shotTargetMode == ManualShotTargetMode.TargetPoint,
            _ => false
        };
    }

    private void ApplyShot(out float kick, out float chip)
    {
        if (_shotMode == ManualShotMode.Kick)
        {
            kick = _kickSpeedMps;
            chip = 0f;
            return;
        }

        kick = 0f;
        chip = _chipDistanceMeters;
    }

    private Angle ResolveBallSkillAngle(Robot.Robot robot)
    {
        if (_shotTargetMode == ManualShotTargetMode.OpenAngle)
        {
            return OpenAngle.CalculateOpenAngleToGoal(Context.Ball.State.Position, robot).Center;
        }

        return Context.Ball.State.Position.AngleWith(_targetPoint);
    }
}