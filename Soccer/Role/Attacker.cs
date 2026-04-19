using Tyr.Common.Time;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record Attacker : IRole
{
    public enum PlayMode
    {
        Attacking,
        Defending
    }

    public PlayMode Mode { get; init; } = PlayMode.Attacking;
    public bool AllowPassing { get; init; } = true;

    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.Attacker(robot);
    }

    public float Importance => 5f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        return Context.Knowledge.GetAttackerAssignmentCost(robot);
    }
}