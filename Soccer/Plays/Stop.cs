using Tyr.Soccer.Knowledge;

namespace Tyr.Soccer.Plays;

public class Stop : IPlay
{
    public static bool IsApplicable() => Context.Referee.Stop();

    public IReadOnlyList<Role.IRole> Tick()
    {
        var roles = new List<Role.IRole>();

        roles.Add(new Role.Goalie());
        roles.Add(new Role.Def(1));
        roles.Add(new Role.Def(2));
        roles.Add(new Role.StopWall());

        var sortedZones = new Queue<Zone>();
        sortedZones.Clear();
        foreach (var zone in Context.Knowledge.Zones.OrderByDescending(z => z.ScoreOffense))
        {
            sortedZones.Enqueue(zone);
        }

        // Subtract 4 (Goalie, Def1, Def2, StopWall)
        for (var i = 0;
             i < Context.Knowledge.OwnRobotsCount - 4 &&
             sortedZones.Count > 0;
             i++)
        {
            roles.Add(new Role.Supporter() { Zone = sortedZones.Dequeue() });
        }

        return roles;
    }
}
