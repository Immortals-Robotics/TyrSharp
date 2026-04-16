using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class TheirPenaltyShootout : IPlay
{
    public static bool IsApplicable() => Context.Referee.TheirPenaltyKick();

    public Formation Tick()
    {
        var requiredRoles = new List<IRole>();
        
        // Goalie is always first
        requiredRoles.Add(new Goalie());

        // Everyone else waits in the dedicated spots (further back for their penalty)
        int waiterCount = Context.OwnRobots.Count - 1; // Minus Goalie
        for (int i = 1; i <= waiterCount; i++)
        {
            requiredRoles.Add(new PenaltyWaiter(i, false));
        }

        return new Formation
        {
            RequiredRoles = requiredRoles
        };
    }
}
