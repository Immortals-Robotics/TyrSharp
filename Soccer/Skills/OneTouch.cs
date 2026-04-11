using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public sealed partial class OneTouch : ISkill
{
    [ConfigEntry] private static float Beta { get; set; } = 0.4f;
    [ConfigEntry] private static float Gamma { get; set; } = 0.14f;
    [ConfigEntry] private static float ShootK { get; set; } = 4000f;

    public Vector2? TargetPoint { get; set; }
    public float Kick { get; set; }
    public float Chip { get; set; }

    public void Execute(Robot.Robot robot)
    {
        var target = TargetPoint ?? Context.Field.OppGoal();
        var pos = CalculatePassPos(target, robot.Position, 78f);

        if (TargetPoint.HasValue)
        {
            robot.TargetAngle = robot.Position.AngleWith(TargetPoint.Value);
        }
        else
        {
            robot.TargetAngle = CalculateOneTouchAngle(robot, pos);
        }

        robot.Navigate(pos, VelocityProfile.Kharaki);
        robot.Shoot = Kick;
        robot.Chip = Chip;
    }

    private static Vector2 CalculatePassPos(Vector2 target, Vector2 staticPos, float bar)
    {
        var ballVelocity = Context.Ball.State.Velocity.Xy();
        if (Utils.ApproximatelyZero(ballVelocity.LengthSquared()))
        {
            return staticPos;
        }

        var ballLine = Line.FromPointAndAngle(Context.Ball.State.Position, ballVelocity.ToAngle());
        var angleWithTarget = staticPos.AngleWith(target);
        var ans = ballLine.ClosestPoint(staticPos + angleWithTarget.ToUnitVec() * bar);
        return ans - angleWithTarget.ToUnitVec() * bar;
    }

    private static Angle CalculateOneTouchAngle(Robot.Robot robot, Vector2 oneTouchPosition)
    {
        var v0 = Context.Ball.State.Velocity.Xy();
        var targetLine = Context.Field.OppGoalLine();
        var openAngle = SkillMath.CalculateOpenAngleToGoal(oneTouchPosition, robot);
        var ballLine = Line.FromPointAndAngle(oneTouchPosition, openAngle.Center);
        var goal = Geometry.Intersection(ballLine, targetLine) ?? new Vector2(Context.Field.OppGoal().X, 0f);

        var sign = Utils.SignInt(goal.X);
        var aa = -sign * 90f;
        var maxError = float.MaxValue;
        var bestAngle = aa;

        while (aa < 180f - 90f * sign)
        {
            var a = aa * MathF.PI / 180f;
            var sinA = MathF.Sin(a);
            var cosA = MathF.Cos(a);
            var dot = v0.X * cosA + v0.Y * sinA;
            var lateral = -sinA * v0.X + cosA * v0.Y;

            var v1x = Beta * lateral * (-sinA) + ShootK * cosA + Gamma * (v0.X - 2f * dot * cosA);
            var v1y = Beta * lateral * cosA + ShootK * sinA + Gamma * (v0.Y - 2f * dot * sinA);
            var error = MathF.Abs(v1x * (-robot.Position.Y + goal.Y) + v1y * (robot.Position.X - goal.X));

            if (error < maxError)
            {
                maxError = error;
                bestAngle = aa;
            }

            aa += 1f;
        }

        return Angle.FromDeg(bestAngle);
    }
}
