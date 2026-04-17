using System.Numerics;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record Mark(FilteredRobot Opponent, Vector2? StaticPosition = null) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Mark
        {
            Robot = robot,
            Opponent = Opponent,
        };
    }

    public float Importance => 0.7f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var cost = Context.Knowledge.CalculateMarkCost(robot.Id, (int)Opponent.Id.Id!);
        return cost < 0 ? DeltaTime.MaxValue : DeltaTime.FromSeconds(cost);
    }
}
