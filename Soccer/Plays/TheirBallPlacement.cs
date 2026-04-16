using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class TheirBallPlacement : IPlay
{
    public static bool IsApplicable() => Context.Referee.TheirBallPlacement();

    public Formation Tick()
    {
        var requiredRoles = new List<IRole>();
        var desiredRoles = new List<IRole>();
        requiredRoles.Add(new Goalie());
        requiredRoles.Add(new Defender(1));
        requiredRoles.Add(new Defender(2));

        // One robot stays clear of the ball and manages the distance
        requiredRoles.Add(new Role.StopWall());

        // The rest are supporters in offensive zones
        var zones = Context.Knowledge.Zones.OrderByDescending(z => z.ScoreOffense).ToList();
        int zoneIdx = 0;
        while (requiredRoles.Count + desiredRoles.Count < Context.OwnRobots.Count)
        {
            if (zoneIdx < zones.Count)
                desiredRoles.Add(new Supporter { Zone = zones[zoneIdx++] });
            else
                break;
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
