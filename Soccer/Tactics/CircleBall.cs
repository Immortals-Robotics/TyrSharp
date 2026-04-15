using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class CircleBall : ITactic
{
    [ConfigEntry] private static float VeryFarBallDis { get; set; } = 600.0f;
    [ConfigEntry] private static float FarBallDis { get; set; } = 160.0f;
    [ConfigEntry] private static DeltaTime FarToNearHys { get; set; } = DeltaTime.FromMilliseconds(50);
    [ConfigEntry] private static float NearAngleTol { get; set; } = 4.0f;
    [ConfigEntry] private static DeltaTime NearToKickHys { get; set; } = DeltaTime.FromMilliseconds(30);
    [ConfigEntry] private static float ShmitCoeff { get; set; } = 1.2f;
    [ConfigEntry] private static float DefaultNearBallDis { get; set; } = 140.0f;

    public required Angle TargetAngle { get; init; }
    public float ShootPower { get; set; }
    public float ChipPower { get; set; }
    public float? NearDistanceOverride { get; set; }

    public Robot.Robot Robot { get; }

    public enum State
    {
        VeryFar,
        Far,
        Near,
        Kick
    }

    private readonly Fsm<State> _fsm;

    public CircleBall(Robot.Robot robot)
    {
        Robot = robot;

        _fsm = new Fsm<State>(State.VeryFar);
        _fsm.AddState(new VeryFarState());
        _fsm.AddState(new FarState(this));
        _fsm.AddState(new NearState(this));
        _fsm.AddState(new KickState(this));

        _fsm.AddTransition(State.VeryFar, State.Far, () =>
            Vector2.Distance(Robot.Position, Context.Ball.State.Position) < VeryFarBallDis);

        _fsm.AddTransition(State.Far, State.VeryFar, () =>
            Vector2.Distance(Robot.Position, Context.Ball.State.Position) > VeryFarBallDis * ShmitCoeff);

        _fsm.AddTransition(State.Far, State.Near,
            () => Vector2.Distance(Robot.Position, Context.Ball.State.Position) < FarBallDis,
            FarToNearHys);

        _fsm.AddTransition(State.Near, State.Far, () =>
            Vector2.Distance(Robot.Position, Context.Ball.State.Position) > FarBallDis * ShmitCoeff);

        _fsm.AddTransition(State.Near, State.Kick, () =>
        {
            var toRobot = (Robot.Position - Context.Ball.State.Position).ToAngle();
            var newToRobot = toRobot - TargetAngle;
            var deltaAngle = Angle.FromDeg(Math.Min(Math.Abs(newToRobot.DegNormalized), 30.0f));

            return Math.Abs(deltaAngle.Deg) < NearAngleTol && (ShootPower > 0 || ChipPower > 0);
        }, NearToKickHys);

        _fsm.AddTransition(State.Kick, State.Far, () =>
            Vector2.Distance(Robot.Position, Context.Ball.State.Position) >
            (NearDistanceOverride ?? DefaultNearBallDis) * ShmitCoeff);
    }

    public CircleBall(Robot.Robot robot, Angle targetAngle, float shootPower, float chipPower,
        float? nearDistanceOverride = null) : this(robot)
    {
        TargetAngle = targetAngle;
        ShootPower = shootPower;
        ChipPower = chipPower;
        NearDistanceOverride = nearDistanceOverride;
    }

    public ISkill? Tick()
    {
        _fsm.DrawDebug(Robot);
        return _fsm.Tick();
    }

    private sealed class VeryFarState : IState<State>
    {
        public State Type => State.VeryFar;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill Tick() => new GoToPoint
        {
            Target = Context.Ball.State.Position,
            LookAt = Context.Ball.State.Position,
            VelocityProfile = VelocityProfile.Mamooli with { Speed = 900.0f }
        };
    }

    private sealed class FarState(CircleBall tactic) : IState<State>
    {
        public State Type => State.Far;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill Tick()
        {
            var angleWithBall = (tactic.Robot.Position - Context.Ball.State.Position).ToAngle();
            var targetPoint = Context.Ball.State.Position.CircleAroundPoint(angleWithBall,
                tactic.NearDistanceOverride ?? DefaultNearBallDis);

            return new GoToPoint
            {
                Target = targetPoint,
                LookAt = Context.Ball.State.Position,
                VelocityProfile = VelocityProfile.Aroom
            };
        }
    }

    private sealed class NearState(CircleBall tactic) : IState<State>
    {
        public State Type => State.Near;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill Tick()
        {
            var toRobot = (tactic.Robot.Position - Context.Ball.State.Position).ToAngle();
            var newToRobot = toRobot - tactic.TargetAngle;
            var deltaAngle = Angle.FromDeg(Math.Min(Math.Abs(newToRobot.DegNormalized), 30.0f));

            var newToRobotDeg = Math.Max(0.0f, Math.Abs(newToRobot.DegNormalized) - deltaAngle.Deg) *
                                Math.Sign(newToRobot.DegNormalized);
            newToRobot = Angle.FromDeg(newToRobotDeg) + tactic.TargetAngle;

            var targetPoint = Context.Ball.State.Position.CircleAroundPoint(newToRobot,
                (tactic.NearDistanceOverride ?? DefaultNearBallDis) / deltaAngle.Cos());

            return new GoToPoint
            {
                Target = targetPoint,
                LookAt = Context.Ball.State.Position,
                VelocityProfile = tactic.NearDistanceOverride.HasValue ? VelocityProfile.Aroom : VelocityProfile.Mamooli
            };
        }
    }

    private sealed class KickState(CircleBall tactic) : IState<State>
    {
        public State Type => State.Kick;

        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public ISkill Tick() => new CircleKick
        {
            TargetAngle = tactic.TargetAngle,
            ShootPower = tactic.ShootPower,
            ChipPower = tactic.ChipPower
        };
    }
}
