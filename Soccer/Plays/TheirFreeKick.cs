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
        var requiredRoles = new List<IRole>();
        var desiredRoles = new List<IRole>();

        // Goalkeeper
        requiredRoles.Add(new Goalie());

        // Essential defenders
        requiredRoles.Add(new Defender(1));
        requiredRoles.Add(new Defender(2));

        // Attacker becomes a wall
        requiredRoles.Add(new DefenceWall());

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

        // Marks and supporters fill whatever robots are actually on the field; they are not required,
        // so a missing robot costs nothing in the assignment instead of an unfilled-role penalty.
        while (requiredRoles.Count + desiredRoles.Count < Context.Knowledge.OwnRobotsCount)
        {
            if (oppIdx < oppsToMark.Count)
            {
                desiredRoles.Add(new Mark(oppsToMark[oppIdx++]));
            }
            else
            {
                if (zones.Count > 0)
                {
                    desiredRoles.Add(new Supporter { Zone = zones.Dequeue() });
                }
                else if (Context.Knowledge.Zones.Count > 0)
                {
                    desiredRoles.Add(new Supporter { Zone = Context.Knowledge.Zones[0] });
                }
                else
                {
                    break;
                }
            }
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
