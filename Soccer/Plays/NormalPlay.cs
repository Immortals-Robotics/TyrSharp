namespace Tyr.Soccer.Plays;

public class NormalPlay : IPlay
{
    public static bool IsApplicable() => Context.Referee.Running();

    public Formation Tick()
    {
        var isDefending = Context.Knowledge.IsDefending;

        var requiredRoles = new List<Role.IRole>
        {
            new Role.Goalie(),
            new Role.Defender(1),
            new Role.Defender(2),
            new Role.Attacker
            {
                Mode = isDefending ? Role.Attacker.PlayMode.Defending : Role.Attacker.PlayMode.Attacking,
                AllowPassing = !isDefending
            }
        };

        var desiredRoles = isDefending
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
        var zones = Context.Knowledge.SortedZonesByOffense;
        while (requiredCount + desiredRoles.Count < Context.Knowledge.OwnRobotsCount)
        {
            if (zones.Count == 0)
            {
                break;
            }

            desiredRoles.Add(new Role.Supporter { Zone = zones.Dequeue() });
        }

        return desiredRoles;
    }

    private static List<Role.IRole> BuildDefendingRoles(int requiredCount)
    {
        var desiredRoles = new List<Role.IRole>();
        var offenseZones = Context.Knowledge.SortedZonesByOffense;
        var defenseZones = Context.Knowledge.SortedZonesByDefense;

        var opponentsToMark = new Queue<Tyr.Common.Vision.Data.FilteredRobot>(
            Context.Knowledge.OpponentThreats.Select(x => x.Robot));

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