using Tyr.Common.Config;
using Tyr.Soccer.Knowledge;

namespace Tyr.Soccer.Plays;

[Configurable]
public partial class Stop : IPlay
{
    [ConfigEntry] private static bool MarkInStop { get; set; } = true;

    public static bool IsApplicable() => Context.Referee.Stop();

    public Formation Tick()
    {
        var requiredRoles = new List<Role.IRole>();

        requiredRoles.Add(new Role.Goalie());
        requiredRoles.Add(new Role.Defender(1));
        requiredRoles.Add(new Role.Defender(2));
        requiredRoles.Add(new Role.StopWall());

        var desiredRoles = Context.Knowledge.IsDefending && MarkInStop
            ? BuildDefendingRoles(requiredRoles.Count)
            : BuildAttackingRoles(requiredRoles.Count);

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }

    private static List<Role.IRole> BuildAttackingRoles(int requiredCount)
    {
        var desiredRoles = new List<Role.IRole>();
        var sortedZones = Context.Knowledge.SortedZonesByOffense;
        while (requiredCount + desiredRoles.Count < Context.Knowledge.OwnRobotsCount)
        {
            if (sortedZones.Count == 0)
            {
                break;
            }

            desiredRoles.Add(new Role.Supporter { Zone = sortedZones.Dequeue() });
        }

        return desiredRoles;
    }

    private static List<Role.IRole> BuildDefendingRoles(int requiredCount)
    {
        var desiredRoles = new List<Role.IRole>();
        var opponentsToMark = new Queue<Tyr.Common.Vision.Data.FilteredRobot>(
            Context.Knowledge.OpponentThreats.Select(x => x.Robot));

        var offenseZones = Context.Knowledge.SortedZonesByOffense;
        var defenseZones = Context.Knowledge.SortedZonesByDefense;
        var sentAttackSupporter = false;

        while (requiredCount + desiredRoles.Count < Context.Knowledge.OwnRobotsCount)
        {
            if (opponentsToMark.Count > 0)
            {
                desiredRoles.Add(new Role.Mark(opponentsToMark.Dequeue()));
                continue;
            }

            if (!sentAttackSupporter && offenseZones.Count > 0)
            {
                desiredRoles.Add(new Role.Supporter { Zone = offenseZones.Dequeue() });
                sentAttackSupporter = true;
                continue;
            }

            if (defenseZones.Count > 0)
            {
                desiredRoles.Add(new Role.Supporter { Zone = defenseZones.Dequeue() });
                continue;
            }

            if (Context.Knowledge.Zones.Count > 0)
            {
                desiredRoles.Add(new Role.Supporter { Zone = Context.Knowledge.Zones[0] });
                continue;
            }

            break;
        }

        return desiredRoles;
    }
}
