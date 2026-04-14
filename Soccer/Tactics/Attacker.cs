using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class Attacker : ITactic
{
    [ConfigEntry] private static float KickSpeed { get; set; } = 6500.0f;
    [ConfigEntry] private static float ChipSpeed { get; set; } = 2000.0f;

    public Robot.Robot Robot { get; private set; }

    public enum State
    {
        None,
        Interception,
        WaitForBall,
        TurnAndShoot,
        Kick
    }

    private readonly Fsm<State> _fsm;

    public Attacker(Robot.Robot robot)
    {
        Robot = robot;

        _fsm = new Fsm<State>(State.None);
        _fsm.AddState(new NoneState());
        _fsm.AddTransition(State.None, State.Kick, () =>
        {
            var ballVelocity = Context.Ball.State.Velocity.Xy().Length();
            return ballVelocity <= 1000.0f || IsRollingKickFeasible(Robot);
        });
        _fsm.AddTransition(State.None, State.WaitForBall, () =>
            InterceptV2.HasImminentImpact(Robot));
        _fsm.AddTransition(State.None, State.Interception, () => true);

        _fsm.AddState(new InterceptionState());
        _fsm.AddTransition(State.Interception, State.Kick, () =>
            Context.Ball.State.Velocity.Xy().Length() <= 100.0f);
        _fsm.AddTransition(State.Interception, State.WaitForBall, () =>
            InterceptV2.HasImminentImpact(Robot));

        _fsm.AddState(new WaitForBallState());
        _fsm.AddTransition(State.WaitForBall, State.TurnAndShoot, () =>
        {
            if (IsBallTowardsMe(Robot) && GetBallLineDistance(Robot) <= 1000.0f) return false;
            var ballRolling = Context.Ball.State.Velocity.Xy().Length() > 1000.0f;
            var ballClose = Vector2.Distance(Context.Ball.State.Position, Robot.Position) < 300.0f;
            return ballClose && !ballRolling;
        });
        _fsm.AddTransition(State.WaitForBall, State.Kick, () =>
        {
            if (IsBallTowardsMe(Robot) && GetBallLineDistance(Robot) <= 1000.0f) return false;
            var ballRolling = Context.Ball.State.Velocity.Xy().Length() > 1000.0f;
            return !ballRolling || IsRollingKickFeasible(Robot);
        });
        _fsm.AddTransition(State.WaitForBall, State.Interception, () =>
        {
            if (IsBallTowardsMe(Robot) && GetBallLineDistance(Robot) <= 1000.0f) return false;
            return true;
        });

        _fsm.AddState(new TurnAndShootState());
        _fsm.AddTransition(State.TurnAndShoot, State.Interception, () =>
        {
            var ballFar = Vector2.Distance(Context.Ball.State.Position, Robot.Position) > 500.0f;
            var ballRolling = Context.Ball.State.Velocity.Xy().Length() > 1000.0f;
            return ballFar && ballRolling;
        });
        _fsm.AddTransition(State.TurnAndShoot, State.Kick, () =>
        {
            var ballFar = Vector2.Distance(Context.Ball.State.Position, Robot.Position) > 500.0f;
            return ballFar;
        });

        _fsm.AddState(new KickState(Robot));
        _fsm.AddTransition(State.Kick, State.Interception, () =>
        {
            var ballVelocity = Context.Ball.State.Velocity.Xy().Length();
            var ballRolling = ballVelocity > 1000.0f;
            var ballTooFast = ballVelocity > 5000.0f;
            var ballTooFar = Vector2.Distance(Context.Ball.State.Position, Robot.Position) > 250.0f;

            return ballTooFast || (ballRolling && !IsRollingKickFeasible(Robot) && ballTooFar);
        });
    }

    public ISkill? Tick()
    {
        return _fsm.Tick();
    }

    private static Vector2 GetBallToGoal()
    {
        var oppGoal = Context.Field.OppGoal();
        return Vector2.Normalize(oppGoal - Context.Ball.State.Position);
    }

    private static bool IsBallTowardsMe(Robot.Robot robot)
    {
        var toBall = Context.Ball.State.Position - robot.Position;
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f) return false;

        float angleDiff = MathF.Abs(Context.Ball.State.Velocity.Xy().AngleDiff(-toBall).DegNormalized);
        return angleDiff < 90.0f;
    }

    private static float GetBallLineDistance(Robot.Robot robot)
    {
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f)
            return Vector2.Distance(Context.Ball.State.Position, robot.Position);

        var ballLine = Line.FromPointAndAngle(Context.Ball.State.Position, Context.Ball.State.Velocity.Xy().ToAngle());
        return ballLine.Distance(robot.Position);
    }

    private static bool IsRollingKickFeasible(Robot.Robot robot)
    {
        var ballToGoal = GetBallToGoal();
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f) return false;

        float angleDiff = MathF.Abs(Context.Ball.State.Velocity.Xy().AngleDiff(ballToGoal).DegNormalized);
        bool ballRollingTowardsGoal = Context.Ball.State.Velocity.Xy().Length() > 0.0f && angleDiff < 15.0f;
        return ballRollingTowardsGoal && !IsBallTowardsMe(robot);
    }

    private sealed class NoneState : IState<State>
    {
        public State Type => State.None;

        public void Enter()
        {
        }

        public ISkill? Tick() => null;

        public void Exit()
        {
        }
    }

    private sealed class InterceptionState : IState<State>
    {
        public State Type => State.Interception;

        public void Enter()
        {
        }

        public ISkill Tick() => new InterceptV2();

        public void Exit()
        {
        }
    }

    private sealed class WaitForBallState : IState<State>
    {
        public State Type => State.WaitForBall;

        public void Enter()
        {
        }

        public ISkill Tick()
        {
            var oppGoal = Context.Field.OppGoal();
            var targetToGoal = Vector2.Normalize(oppGoal - Context.Ball.State.Position);

            float angleDiff = MathF.Abs((-Context.Ball.State.Velocity.Xy()).AngleDiff(targetToGoal).DegNormalized);

            if (angleDiff < 60.0f)
            {
                return new OneTouch { Kick = KickSpeed, Chip = ChipSpeed };
            }

            return new WaitForBall { StaticPosition = Context.Ball.State.Position + targetToGoal * 500f };
        }

        public void Exit()
        {
        }
    }

    private sealed class TurnAndShootState : IState<State>
    {
        public State Type => State.TurnAndShoot;

        public void Enter()
        {
        }

        public ISkill Tick() => new TurnAndShoot
        {
            Angle = GetBallToGoal().ToAngle(),
            Kick = KickSpeed,
            Chip = ChipSpeed
        };

        public void Exit()
        {
        }
    }

    private sealed class KickState(Robot.Robot robot) : IState<State>
    {
        public State Type => State.Kick;

        public void Enter()
        {
        }

        public ISkill Tick()
        {
            var ballToGoal = GetBallToGoal();
            var angleCorrect = MathF.Abs(robot.Angle.ToUnitVec().AngleDiff(ballToGoal).DegNormalized) < 5.0f;
            var kick = angleCorrect ? KickSpeed : 1.0f;
            var chip = angleCorrect ? ChipSpeed : 0.0f;
            return new KickBall { Angle = ballToGoal.ToAngle(), Kick = kick, Chip = chip };
        }

        public void Exit()
        {
        }
    }
}
