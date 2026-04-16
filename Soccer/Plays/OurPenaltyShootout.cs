using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurPenaltyShootout : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurPenaltyKick();

    public IReadOnlyList<IRole> Tick()
    {
        var roles = new List<IRole>();
        
        // Goalie is always first
        roles.Add(new Goalie());

        // Penalty Kicker is the one closest to the ball
        roles.Add(new PenaltyKicker());

        // Everyone else waits in the dedicated spots
        int waiterCount = Context.OwnRobots.Count - 2; // Minus Goalie and Kicker
        for (int i = 1; i <= waiterCount; i++)
        {
            roles.Add(new PenaltyWaiter(i, true));
        }

        return roles;
    }
}
