using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class TheirPenaltyShootout : IPlay
{
    public static bool IsApplicable() => Context.Referee.TheirPenaltyKick();

    public IReadOnlyList<IRole> Tick()
    {
        var roles = new List<IRole>();
        
        // Goalie is always first
        roles.Add(new Goalie());

        // Everyone else waits in the dedicated spots (further back for their penalty)
        int waiterCount = Context.OwnRobots.Count - 1; // Minus Goalie
        for (int i = 1; i <= waiterCount; i++)
        {
            roles.Add(new PenaltyWaiter(i, false));
        }

        return roles;
    }
}
