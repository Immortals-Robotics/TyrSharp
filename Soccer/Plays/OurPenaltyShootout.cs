using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurPenaltyShootout : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurPenaltyKick();

    public Formation Tick()
    {
        var requiredRoles = new List<IRole>();
        
        // Goalie is always first
        requiredRoles.Add(new Goalie());

        // Penalty Kicker is the one closest to the ball
        requiredRoles.Add(new PenaltyKicker());

        // Everyone else who is actually on the field waits in the dedicated spots
        var desiredRoles = new List<IRole>();
        int waiterCount = Context.Knowledge.OwnRobotsCount - 2; // Minus Goalie and Kicker
        for (int i = 1; i <= waiterCount; i++)
        {
            desiredRoles.Add(new PenaltyWaiter(i, true));
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
