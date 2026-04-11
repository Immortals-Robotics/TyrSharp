using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Skills;
using Tyr.Soccer.Tactics.Fsm;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class Attacker : ITactic
{
    public enum State
    {
        None,
        Interception,
        WaitForBall,
        TurnAndShoot,
        Kick
    }

    private readonly TacticStateMachine<State> _stateMachine;
    private int _lastRobotId = -1;

    [ConfigEntry] private static float KickSpeed { get; set; } = 6500.0f;
    [ConfigEntry] private static float ChipSpeed { get; set; } = 2000.0f;

    public Attacker()
    {
        var builder = new TacticStateMachineBuilder<State>();

        builder.Configure(State.None)
            .TransitionTo(State.Kick).When(robot =>
            {
                var ballVelocity = Context.Ball.State.Velocity.Xy().Length();
                return ballVelocity <= 1000.0f || IsRollingKickFeasible(robot);
            })
            .TransitionTo(State.WaitForBall).When(robot => IsBallTowardsMe(robot) && GetBallLineDistance(robot) < 200.0f)
            .TransitionTo(State.Interception).When(_ => true)
            .OnTick(_ => new Halt());

        builder.Configure(State.Interception)
            .TransitionTo(State.Kick).When(_ => Context.Ball.State.Velocity.Xy().Length() <= 100.0f)
            .TransitionTo(State.WaitForBall).When(robot => IsBallTowardsMe(robot) && GetBallLineDistance(robot) < 200.0f)
            .OnTick(_ => new InterceptBall());

        builder.Configure(State.WaitForBall)
            .TransitionTo(State.TurnAndShoot).When(robot => 
            {
                if (IsBallTowardsMe(robot) && GetBallLineDistance(robot) <= 1000.0f) return false;
                var ballRolling = Context.Ball.State.Velocity.Xy().Length() > 1000.0f;
                var ballClose = Vector2.Distance(Context.Ball.State.Position, robot.Position) < 300.0f;
                return ballClose && !ballRolling;
            })
            .TransitionTo(State.Kick).When(robot =>
            {
                if (IsBallTowardsMe(robot) && GetBallLineDistance(robot) <= 1000.0f) return false;
                var ballRolling = Context.Ball.State.Velocity.Xy().Length() > 1000.0f;
                return !ballRolling || IsRollingKickFeasible(robot);
            })
            .TransitionTo(State.Interception).When(robot =>
            {
                if (IsBallTowardsMe(robot) && GetBallLineDistance(robot) <= 1000.0f) return false;
                return true;
            })
            .OnTick(robot => 
            {
                var targetToGoal = GetBallToGoal();
                float angleDiff = MathF.Abs((float)(-Context.Ball.State.Velocity.Xy()).AngleDiff(targetToGoal).DegNormalized);
                
                if (angleDiff < 60.0f)
                {
                    return new OneTouch { Kick = KickSpeed, Chip = ChipSpeed };
                }
                
                return new WaitForBall { StaticPosition = Context.Ball.State.Position + targetToGoal * 500f }; 
            }); 

        builder.Configure(State.TurnAndShoot)
            .TransitionTo(State.Interception).When(robot =>
            {
                var ballFar = Vector2.Distance(Context.Ball.State.Position, robot.Position) > 500.0f;
                var ballRolling = Context.Ball.State.Velocity.Xy().Length() > 1000.0f;
                return ballFar && ballRolling;
            })
            .TransitionTo(State.Kick).When(robot =>
            {
                var ballFar = Vector2.Distance(Context.Ball.State.Position, robot.Position) > 500.0f;
                return ballFar;
            })
            .OnTick(_ => new TurnAndShoot { Angle = GetBallToGoal().ToAngle(), Kick = KickSpeed, Chip = ChipSpeed }); 

        builder.Configure(State.Kick)
            .TransitionTo(State.Interception).When(robot =>
            {
                var ballVelocity = Context.Ball.State.Velocity.Xy().Length();
                var ballRolling = ballVelocity > 1000.0f;
                var ballTooFast = ballVelocity > 5000.0f;
                var ballTooFar = Vector2.Distance(Context.Ball.State.Position, robot.Position) > 250.0f;

                return ballTooFast || (ballRolling && !IsRollingKickFeasible(robot) && ballTooFar);
            })
            .OnTick(robot => 
            {
                var ballToGoal = GetBallToGoal();
                var angleCorrect = MathF.Abs((float)robot.Angle.ToUnitVec().AngleDiff(ballToGoal).DegNormalized) < 5.0f;
                var kick = angleCorrect ? KickSpeed : 1.0f;
                var chip = angleCorrect ? ChipSpeed : 0.0f;
                return new KickBall { Angle = ballToGoal.ToAngle(), Kick = kick, Chip = chip };
            }); 

        _stateMachine = builder.Build();
    }

    public void Reset()
    {
        _stateMachine.Reset(State.None);
        _lastRobotId = -1;
    }

    public ISkill Tick(Robot.Robot robot)
    {
        if (_lastRobotId != robot.Id)
        {
            _stateMachine.Reset(State.None);
        }
        _lastRobotId = robot.Id;

        return _stateMachine.Tick(robot);
    }

    private Vector2 GetBallToGoal()
    {
        var oppGoal = Context.Field.OppGoal();
        return Vector2.Normalize(oppGoal - Context.Ball.State.Position);
    }

    private bool IsBallTowardsMe(Robot.Robot robot)
    {
        var toBall = Context.Ball.State.Position - robot.Position;
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f) return false;
        
        float angleDiff = MathF.Abs((float)Context.Ball.State.Velocity.Xy().AngleDiff(-toBall).DegNormalized);
        return angleDiff < 90.0f;
    }

    private float GetBallLineDistance(Robot.Robot robot)
    {
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f)
            return Vector2.Distance(Context.Ball.State.Position, robot.Position);
            
        var ballLine = Line.FromPointAndAngle(Context.Ball.State.Position, Context.Ball.State.Velocity.Xy().ToAngle());
        return (float)ballLine.Distance(robot.Position);
    }

    private bool IsRollingKickFeasible(Robot.Robot robot)
    {
        var ballToGoal = GetBallToGoal();
        if (Context.Ball.State.Velocity.LengthSquared() < 0.001f) return false;
        
        float angleDiff = MathF.Abs((float)Context.Ball.State.Velocity.Xy().AngleDiff(ballToGoal).DegNormalized);
        bool ballRollingTowardsGoal = Context.Ball.State.Velocity.Xy().Length() > 0.0f && angleDiff < 15.0f;
        return ballRollingTowardsGoal && !IsBallTowardsMe(robot);
    }
}
