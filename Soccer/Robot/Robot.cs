using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;
using Tyr.Common.Math;
using Tyr.Common.Sender.Data;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Robot;

[Configurable]
public partial class Robot
{
    [ConfigEntry("Minimum Dribbler force to accept ball contact")]
    private static float DribblerDetectForce { get; set; } = 1.0f;

    private float _shoot;
    private float _chip;
    private float _dribblerSpeed;
    private float _dribblerForce;

    public FilteredRobot Filtered { get; set; }
    public RobotState State => Filtered.State;

    public bool Seen => !Utils.ApproximatelyZero(Filtered.Quality);
    public int Id => (int)Filtered.Id.Id!;
    public Vector2 Position => State.Position;
    public Vector2 Velocity => State.Velocity;
    public Angle Angle => State.Angle;
    public Angle AngularVelocity => State.AngularVelocity;

    public float CenterToDribbler => new Tyr.Common.Math.Shapes.Robot
    {
        Radius = Context.RobotRadius,
        CenterToDribbler = PhysicalStatus.CenterToDribbler
    }.EffectiveCenterToDribbler;

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

    public float DribblerSpeed
    {
        get => _dribblerSpeed;
        set => _dribblerSpeed = PhysicalStatus.HasDribbler ? value : 0f;
    }

    public float DribblerForce
    {
        get => _dribblerForce;
        set => _dribblerForce = PhysicalStatus.HasDribbler ? value : 0f;
    }

    public bool Halted { get; private set; }

    // Never use this {Death}{Death}
    // TODO: Remove and handle it in higher level
    public Role.IRole? Role { get; set; }

    public Vector2 TargetPosition { get; private set; }
    public Angle TargetAngle { get; set; }

    public float DynamicBallObstacleRadius { get; set; }
    public bool IgnoreRefereeBallObstacle { get; set; }

    public PhysicalStatus PhysicalStatus => PhysicalStatus.StatusArray[Id];

    public HardwareStatus HardwareStatus { get; } = new();

    public bool HasBallContact
    {
        get
        {
            // 1. Simulation feedback (grSim sensor)
            if (HardwareStatus.SimBallContact.HasValue)
                return HardwareStatus.SimBallContact.Value;

            // 2. Real robot force sensor
            return HardwareStatus.DribblerFeedback?.ActualForceN >= DribblerDetectForce &&
                   HardwareStatus.IrSensor?.Blocked == true;
        }
    }

    public void Reset()
    {
        _shoot = 0f;
        _chip = 0f;
        _dribblerSpeed = 0f;
        _dribblerForce = 0f;
        Halted = false;
        Navigated = false;
        IgnoreRefereeBallObstacle = false;
    }

    public void Halt()
    {
        TargetAngle = Angle;
        TargetPosition = Position;

        Trajectory = new Trajectory2D();

        _shoot = 0.0f;
        _chip = 0.0f;
        _dribblerSpeed = 0.0f;
        _dribblerForce = 0.0f;

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
        DribblerSpeed = DribblerSpeed,
        DribblerForce = DribblerForce,
    };

    public void SetDribbler(float speed, float force)
    {
        DribblerSpeed = speed;
        DribblerForce = force;
    }
}
