using System.Numerics;
using Tyr.Soccer.Tactics;

namespace Tyr.Soccer.Role;

public record BallPlacer(int PlacerId) : IRole
{
    public ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.BallPlacement(robot, PlacerId);
    }

    public float Importance => 100f;

    public float CostFor(Robot.Robot robot)
    {
        var ball = Context.Ball.State.Position;
        var target = Context.Referee.DesignatedPosition();
        var toTarget = target - ball;

        if (toTarget.Length() < 10.0f)
        {
            return Vector2.Distance(robot.Position, ball);
        }

        var dir = Vector2.Normalize(toTarget);
        var distance = Context.Field.RobotRadius * 2.0f;

        // Placer 1 is closer to target, Placer 2 is further back
        var targetPos = PlacerId == 1 ? ball + dir * distance : ball - dir * distance;
        return Vector2.Distance(robot.Position, targetPos);
    }
}
