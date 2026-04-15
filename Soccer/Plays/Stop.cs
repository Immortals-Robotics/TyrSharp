using Tyr.Soccer.Knowledge;

namespace Tyr.Soccer.Plays;

public class Stop : IPlay
{
    public static bool IsApplicable() => Context.Referee.Stop();

    public IReadOnlyList<Role.IRole> Tick()
    {
        var roles = new List<Role.IRole>();

        roles.Add(new Role.Goalie());
        roles.Add(new Role.StopWall());

        var sortedZones = new Queue<Zone>();
        sortedZones.Clear();
        foreach (var zone in Context.Knowledge.Zones.OrderByDescending(z => z.Score))
        {
            sortedZones.Enqueue(zone);
        }

        //TODO: This should exclude Goalie, attacker, defenders dynamically at some point 
        for (var i = 0;
             i < Context.Knowledge.OwnRobotsCount - 2 &&
             sortedZones.Count > 0;
             i++)
        {
            roles.Add(new Role.Supporter() { Zone = sortedZones.Dequeue() });
        }

        return roles;
    }
}
