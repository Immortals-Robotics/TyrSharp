using System.Numerics;
using Tyr.Common.Extensions;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class TheirBallPlacement : IPlay
{
    public static bool IsApplicable() => Context.Referee.TheirBallPlacement();

    public IReadOnlyList<IRole> Tick()
    {
        var roles = new List<IRole>();
        roles.Add(new Goalie());
        roles.Add(new Defender(1));
        roles.Add(new Defender(2));

        // One robot stays clear of the ball and manages the distance
        roles.Add(new Role.StopWall());

        // The rest are supporters in offensive zones
        var zones = Context.Knowledge.Zones.OrderByDescending(z => z.ScoreOffense).ToList();
        int zoneIdx = 0;
        while (roles.Count < Context.OwnRobots.Count)
        {
            if (zoneIdx < zones.Count)
                roles.Add(new Supporter { Zone = zones[zoneIdx++] });
            else
                break;
        }

        return roles;
    }
}
