using System.Collections.Generic;
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
        var oppsToMark = Context.Knowledge.OpponentThreats
            .Select(t => t.Robot)
            .ToList();

        foreach (var opp in oppsToMark)
        {
            Log.ZLogDebug($"opp: {opp.Id}");
        }

        var zones = Context.Knowledge.SortedZonesByDefense;
        int oppIdx = 0;

        while (roles.Count < Context.OwnRobots.Count)
        {
            if (oppIdx < oppsToMark.Count)
            {
                roles.Add(new Mark(oppsToMark[oppIdx++]));
            }
            else
            {
                if (zones.Count > 0)
                {
                    roles.Add(new Supporter { Zone = zones.Dequeue() });
                }
                else if (Context.Knowledge.Zones.Count > 0)
                {
                    roles.Add(new Supporter { Zone = Context.Knowledge.Zones[0] });
                }
                else
                {
                    break;
                }
            }
        }

        return new Formation { RequiredRoles = roles };
    }
}
