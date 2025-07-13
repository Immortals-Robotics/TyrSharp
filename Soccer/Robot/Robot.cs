using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Sender.Data;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Robot;

[Configurable]
public partial class Robot
{
    private float _shoot;
    private float _chip;
    private float _dribbler;
    public FilteredRobot Filtered { get; set; }
    public RobotState State => Filtered.State;

    public bool Seen => !Utils.ApproximatelyZero(Filtered.Quality);
    public int Id => (int)Filtered.Id.Id!;
    public Vector2 Position => State.Position;
    public Vector2 Velocity => State.Velocity;
    public Angle Angle => State.Angle;
    public Angle AngularVelocity => State.AngularVelocity;

    public float Shoot
    {
        get => _shoot;
        set => _shoot = PhysicalStatus.HasDirectKick ? value : 0f;
    }

    public float Chip
    {
        get => _chip;
        set => _chip = PhysicalStatus.HasChipKick ? value : 0f;
    }

    // TODO: remove 16
    public float Dribbler
    {
        get => _dribbler;
        set => _dribbler = PhysicalStatus.HasDribbler ? 16 * value : 0f;
    }

    public bool Halted { get; private set; }

    public Vector2 TargetPosition { get; private set; }
    public Angle TargetAngle { get; private set; }

    public float DynamicBallObstacleRadius { get; set; }

    public PhysicalStatus PhysicalStatus => PhysicalStatus.StatusArray[Id];

    public void Reset()
    {
        _shoot = 0f;
        _chip = 0f;
        _dribbler = 0f;
        Halted = false;
        Navigated = false;
    }

    public void Halt()
    {
        TargetAngle = Angle;
        TargetPosition = Position;

        Trajectory = new Trajectory2D();

        _shoot = 0.0f;
        _chip = 0.0f;
        _dribbler = 0.0f;

        Halted = true;
    }

    public void Face(Vector2 target)
    {
        TargetAngle = Position.AngleWith(target);
    }

    public Command CurrentCommand => new()
    {
        VisionId = Id,
        Halted = Halted,
        Motion = CurrentMotion,
        CurrentAngle = Angle,
        TargetAngle = TargetAngle,
        Shoot = Shoot,
        Chip = Chip,
        Dribbler = Dribbler,
    };
}