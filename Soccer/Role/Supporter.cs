using System.Numerics;
using Tyr.Soccer.Knowledge;

namespace Tyr.Soccer.Role;

public record Supporter : IRole
{
    public required Zone Zone { get; init; }

    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Supporter(robot) { Zone = Zone };
    }

    public float Importance => 0.5f;

    public float CostFor(Robot.Robot robot) => Vector2.Distance(robot.Position, Zone.Rect.Center);
}
