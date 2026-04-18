using System.Numerics;
using Tyr.Common.Time;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record BallPlacer(int PlacerId) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.BallPlacement(robot, PlacerId);
    }

    public float Importance => 100f;
    public DeltaTime StickinessBonus => DeltaTime.FromMilliseconds(600);

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var ball = Context.Ball.State.Position;
        var target = Context.Referee.DesignatedPosition();
        var toTarget = target - ball;

        Vector2 targetPos;
        if (toTarget.Length() < 10.0f)
        {
            targetPos = ball;
        }
        else
        {
            var dir = Vector2.Normalize(toTarget);
            var distance = Context.Field.RobotRadius * 2.0f;

            // Placer 1 is closer to target, Placer 2 is further back
            targetPos = PlacerId == 1 ? ball + dir * distance : ball - dir * distance;
        }

        var trajectory = TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            targetPos, VelocityProfile.Mamooli);
        var baseCost = DeltaTime.FromSeconds(trajectory.Duration);

        // Keep placer ids stable across frames to avoid swapping and resetting the FSM mid-placement.
        if (Context.GetAssignedRole(robot) is BallPlacer previous && previous.PlacerId != PlacerId)
        {
            baseCost += DeltaTime.FromMilliseconds(600);
        }

        return baseCost;
    }
}
