using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public partial class DefenceWall : ISkill
{
    [ConfigEntry] private static float Radius { get; set; } = 550f;

    public bool Kickoff { get; set; }

    public void Execute(Robot.Robot robot)
    {
        var distance = 700.0f + Context.RobotRadius;

        var x = -Context.SideSign * Context.Ball.State.Position.X;
        var tetta = -0.000003f * x * x + 0.0016f * x + 90.0f;
        if (Kickoff)
        {
            tetta = 14.0f;
        }

        Log.ZLogDebug($"wall limit: {tetta}");

        Vector2 target;

        var oppAttack = Context.Knowledge.FindKickerOpp();
        if (oppAttack.HasValue)
        {
            Log.ZLogDebug($"{oppAttack}");

            var direction = Vector2.Normalize(Context.Ball.State.Position - oppAttack.Value.State.Position);
            target = Context.Ball.State.Position + direction * distance;
        }
        else
        {
            target = Context.Ball.State.Position.CircleAroundPoint(
                Context.Ball.State.Position.AngleWith(Context.Field.OwnGoal()),
                distance);
        }

        var ballAngle = Context.Ball.State.Position.AngleWith(target);
        var firstLeg = Context.Ball.State.Position.AngleWith(
            new Vector2(Context.Field.OwnGoal().X, Utils.SignInt(Context.Ball.State.Position.Y) * 350.0f));
        var secLeg = firstLeg - Angle.FromDeg(tetta * Utils.SignInt(Context.Ball.State.Position.Y) * Context.SideSign);

        Log.ZLogDebug($"ball: {ballAngle}    f: {firstLeg}    s: {secLeg}");

        var isOut = false; // Original code has this calculation commented out

        if (isOut)
        {
            target = Context.Ball.State.Position.CircleAroundPoint(
                Context.Ball.State.Position.AngleWith(Context.Field.OwnGoal()),
                Radius + Context.RobotRadius);
        }

        robot.Face(Context.Ball.State.Position);
        robot.Navigate(target, VelocityProfile.Mamooli);
    }
}