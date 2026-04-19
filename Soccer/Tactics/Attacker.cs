using System.Numerics;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

public class Attacker : ITactic
{
    public Robot.Robot Robot { get; }

    public enum State
    {
        None,
        Interception,
        WaitForBall,
        TurnAndShoot,
        Kick
    }

    private readonly Fsm<State> _fsm;

    private Vector2 _targetPosition;
    private float _kick;
    private float _chip;

    public Attacker(Robot.Robot robot)
    {
        Robot = robot;
        ApplyDecision(Context.Knowledge.GetAttackerDecision(robot.Id));

        _fsm = new Fsm<State>(State.None);
        _fsm.AddState(new NoneState());
        _fsm.AddTransition(State.None, State.Kick, () =>
        {
            var ballVelocity = Context.Ball.State.Velocity.Xy().Length();
            return ballVelocity <= 1000.0f || IsRollingKickFeasible();
        });
        _fsm.AddTransition(State.None, State.WaitForBall, () =>
            InterceptV2.HasImminentImpact(Robot));
        _fsm.AddTransition(State.None, State.Interception, () => true);

        _fsm.AddState(new InterceptionState());
        _fsm.AddTransition(State.Interception, State.Kick, () =>
            Context.Ball.State.Velocity.Xy().Length() <= 100.0f);
        _fsm.AddTransition(State.Interception, State.WaitForBall, () =>
            InterceptV2.HasImminentImpact(Robot));

        _fsm.AddState(new WaitForBallState(this));
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
            return !ballRolling || IsRollingKickFeasible();
        });
        _fsm.AddTransition(State.WaitForBall, State.Interception, () =>
        {
            if (IsBallTowardsMe(Robot) && GetBallLineDistance(Robot) <= 1000.0f) return false;
            return true;
        });

        _fsm.AddState(new TurnAndShootState(this));
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

        _fsm.AddState(new KickState(this));
        _fsm.AddTransition(State.Kick, State.Interception, () =>
        {
            var ballVelocity = Context.Ball.State.Velocity.Xy().Length();
            var ballRolling = ballVelocity > 1000.0f;
            var ballTooFast = ballVelocity > 5000.0f;
            var ballTooFar = Vector2.Distance(Context.Ball.State.Position, Robot.Position) > 250.0f;

            return ballTooFast || (ballRolling && !IsRollingKickFeasible() && ballTooFar);
        });
    }

    public ISkill? Tick()
    {
        ApplyDecision(Context.Knowledge.GetAttackerDecision(Robot.Id));
        _fsm.DrawDebug(Robot);
        return _fsm.Tick();
    }

    private void ApplyDecision(Knowledge.Knowledge.AttackerDecision decision)
    {
        _targetPosition = decision.TargetPosition;
        _kick = decision.Kick;
        _chip = decision.Chip;
    }

    private Vector2 GetBallToTargetDirection()
    {
        var ballToTarget = _targetPosition - Context.Ball.State.Position;
        if (ballToTarget.LengthSquared() < 0.001f)
        {
            ballToTarget = Context.Field.OppGoal() - Context.Ball.State.Position;
        }

        if (ballToTarget.LengthSquared() < 0.001f)
        {
            return Robot.Angle.ToUnitVec();
        }

        return Vector2.Normalize(ballToTarget);
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

    private bool IsRollingKickFeasible()
    {
        var ballToTarget = GetBallToTargetDirection();
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f) return false;

        float angleDiff = MathF.Abs(Context.Ball.State.Velocity.Xy().AngleDiff(ballToTarget).DegNormalized);
        bool ballRollingTowardsTarget = Context.Ball.State.Velocity.Xy().Length() > 0.0f && angleDiff < 15.0f;
        return ballRollingTowardsTarget && !IsBallTowardsMe(Robot);
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

    private sealed class WaitForBallState(Attacker tactic) : IState<State>
    {
        public State Type => State.WaitForBall;

        public void Enter()
        {
        }

        public ISkill Tick()
        {
            var targetToGoal = tactic.GetBallToTargetDirection();
            float angleDiff = MathF.Abs((-Context.Ball.State.Velocity.Xy()).AngleDiff(targetToGoal).DegNormalized);

            if (angleDiff < 60.0f)
            {
                return new OneTouch
                {
                    TargetPoint = tactic._targetPosition,
                    Kick = tactic._kick,
                    Chip = tactic._chip
                };
            }

            return new WaitForBall { StaticPosition = Context.Ball.State.Position + targetToGoal * 500f };
        }

        public void Exit()
        {
        }
    }

    private sealed class TurnAndShootState(Attacker tactic) : IState<State>
    {
        public State Type => State.TurnAndShoot;

        public void Enter()
        {
        }

        public ISkill Tick() => new TurnAndShoot
        {
            Angle = tactic.GetBallToTargetDirection().ToAngle(),
            Kick = tactic._kick,
            Chip = tactic._chip
        };

        public void Exit()
        {
        }
    }

    private sealed class KickState(Attacker tactic) : IState<State>
    {
        public State Type => State.Kick;

        public void Enter()
        {
        }

        public ISkill Tick()
        {
            var ballToTarget = tactic.GetBallToTargetDirection();
            var angleCorrect = MathF.Abs(tactic.Robot.Angle.ToUnitVec().AngleDiff(ballToTarget).DegNormalized) < 5.0f;
            var kick = angleCorrect ? tactic._kick : 0.0f;
            var chip = angleCorrect ? tactic._chip : 0.0f;
            return new KickBall { Angle = ballToTarget.ToAngle(), Kick = kick, Chip = chip };
        }

        public void Exit()
        {
        }
    }
}
