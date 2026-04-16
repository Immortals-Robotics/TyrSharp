using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Soccer.Robot;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

public class PenaltyUsShootout : ITactic
{
    public enum State
    {
        Wait,
        MoveForward,
        Shoot
    }

    public Robot.Robot Robot { get; }
    private readonly Fsm<State> _fsm;

    public PenaltyUsShootout(Robot.Robot robot)
    {
        Robot = robot;
        _fsm = new Fsm<State>(State.Wait);

        _fsm.AddState(new WaitState(this));
        _fsm.AddState(new MoveForwardState(this));
        _fsm.AddState(new ShootState(this));

        // Transitions
        _fsm.AddTransition(State.Wait, State.MoveForward, () => Context.Referee.Ready);

        _fsm.AddTransition(State.MoveForward, State.Shoot, () => Vector2.Distance(Context.Ball.State.Position, Context.Field.OppGoal()) <= 5500f);

    }

    public ISkill? Tick()
    {
        _fsm.DrawDebug(Robot);
        return _fsm.Tick();
    }

    private sealed class WaitState(PenaltyUsShootout tactic) : IState<State>
    {
        public State Type => State.Wait;
        public void Enter() { }
        public void Exit() { }
        public ISkill? Tick()
        {
            tactic.Robot.Face(Context.Field.OppGoal());
            return new CircleKick
            {
                TargetAngle = Context.Field.OppGoal().AngleWith(Context.Ball.State.Position),
                ShootPower = 0,
                ChipPower = 0
            };
        }
    }

    private sealed class MoveForwardState(PenaltyUsShootout tactic) : IState<State>
    {
        public State Type => State.MoveForward;
        public void Enter() { }
        public void Exit() { }
        public ISkill? Tick()
        {
            tactic.Robot.Face(Context.Field.OppGoal());
            return new KickBall
            {
                Angle = Context.Field.OppGoal().AngleWith(Context.Ball.State.Position),
                Kick = 1800f,
                Chip = 0
            };
        }
    }

    private sealed class ShootState(PenaltyUsShootout tactic) : IState<State>
    {
        public State Type => State.Shoot;
        public void Enter() { }
        public void Exit() { }
        public ISkill? Tick()
        {
            var openAngle = Context.Knowledge.OpenAngle.CalculateOpenAngleToGoal(Context.Ball.State.Position, tactic.Robot);

            Angle shootAngle;
            if (openAngle.Magnitude.Deg > 1.0f)
            {
                shootAngle = openAngle.Center;
            }
            else
            {
                shootAngle = (Context.Field.OppGoal() - Context.Ball.State.Position).ToAngle();
            }

            return new KickBall
            {
                Angle = shootAngle,
                Kick = 5000f,
                Chip = 0
            };
        }
    }
}
