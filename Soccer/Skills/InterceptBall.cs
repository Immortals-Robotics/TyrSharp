using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public sealed class InterceptBall : ISkill
{
    public Angle Angle { get; set; }
    public float WaitTimeSeconds { get; set; }

    public void Execute(Robot.Robot robot)
    {
        var ballVelocity = Context.Ball.State.Velocity.Xy();
        if (ballVelocity.Length() < 100f)
        {
            robot.Halt();
            return;
        }

        var profile = VelocityProfile.Mamooli;
        var interceptionAngle = ballVelocity.ToAngle();
        var interceptionTime = SkillMath.CalculateBallRobotReachTime(robot, interceptionAngle, profile, WaitTimeSeconds);
        var ballState = SkillMath.PredictBall(DeltaTime.FromSeconds(interceptionTime));
        var interceptionPoint = ballState.Position.CircleAroundPoint(ballVelocity.ToAngle(), Context.RobotRadius);

        robot.Navigate(interceptionPoint, profile, NavigationFlags.BallMediumObstacle);
        robot.TargetAngle = (ballState.Position - interceptionPoint).ToAngle();
    }
}
