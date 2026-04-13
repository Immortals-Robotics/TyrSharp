using Tyr.Common;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Soccer.Helpers;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public sealed class InterceptBall : ISkill
{
    private const float ReceiveDribblerSpeed = 2f;
    private const float ReceiveDribblerForce = 5f;
    private const float DribblerActivationTimeSeconds = 0.2f;

    public Angle Angle { get; set; }
    public float WaitTimeSeconds { get; set; }

    private BallInterception.InterceptPlan? _lastPlan;

    public void Execute(Robot.Robot robot)
    {
        var ballPosition = Context.Ball.State.Position;
        var ballVelocity = Context.Ball.State.Velocity.Xy();

        var trajectory = ServiceLocator.BallTrajectoryFactory.FromState(Context.Ball.State);
        var centerToDribbler = robot.CenterToDribbler;

        var hasPlan = BallInterception.TryFindPlan(
            Context.Ball,
            trajectory,
            robot.Position,
            robot.Velocity,
            robot.TargetAngle,
            VelocityProfile.Mamooli,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            Context.Field.BallRadius,
            centerToDribbler,
            out var plan,
            _lastPlan);

        if (!hasPlan)
        {
            robot.Halt();
            _lastPlan = null;
            return;
        }

        var destination = BallReceiving.ClampToLegalDestination(
            plan.CenterDestination,
            Context.Field.RectangleWithBoundary,
            Context.Field.OwnPenaltyArea(),
            Context.Field.OppPenaltyArea(),
            BallReceiving.PenaltyAreaMargin);

        robot.Navigate(destination, VelocityProfile.Mamooli, NavigationFlags.NoBallObstacle);
        robot.TargetAngle = plan.FacingAngle;

        var dribblerActive = plan.TimeSeconds < DribblerActivationTimeSeconds;
        robot.SetDribbler(
            dribblerActive ? ReceiveDribblerSpeed : 0f,
            dribblerActive ? ReceiveDribblerForce : 0f);

        _lastPlan = plan;
    }
}
