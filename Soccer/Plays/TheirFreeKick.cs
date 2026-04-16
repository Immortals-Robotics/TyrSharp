using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Tyr.Common.Math;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class TheirFreeKick : IPlay
{
    // This play is for defending their free kick.
    // In Tyr it's assigned to theirFreeKick state.
    public static bool IsApplicable() => Context.Referee.TheirFreeKick();

    public Formation Tick()
    {
        var roles = new List<IRole>();
        
        // Goalkeeper
        roles.Add(new Goalie());

        // Essential defenders
        roles.Add(new Defender(1));
        roles.Add(new Defender(2));

        // Attacker becomes a wall
        roles.Add(new DefenceWall());

        // Mids: mark opponents or use zones
        var oppsToMark = Context.OppRobots
            .OrderBy(opp => Vector2.Distance(opp.State.Position, Context.Field.OwnGoal()))
            .ToList();

        var zones = Context.Knowledge.Zones.OrderByDescending(z => z.ScoreDefense).ToList();
        int oppIdx = 0;
        int zoneIdx = 0;

        while (roles.Count < Context.OwnRobots.Count)
        {
            if (oppIdx < oppsToMark.Count)
            {
                roles.Add(new Mark(oppsToMark[oppIdx++]));
            }
            else
            {
                roles.Add(new Supporter { Zone = zones.ElementAtOrDefault(zoneIdx++) ?? Context.Knowledge.Zones[0] });
            }
        }

        return new Formation { RequiredRoles = roles };
    }
}
