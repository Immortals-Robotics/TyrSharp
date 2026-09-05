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

        // Everyone else who is actually on the field waits in the dedicated spots (further back for their penalty)
        var desiredRoles = new List<IRole>();
        int waiterCount = Context.Knowledge.OwnRobotsCount - 1; // Minus Goalie
        for (int i = 1; i <= waiterCount; i++)
        {
            desiredRoles.Add(new PenaltyWaiter(i, false));
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
