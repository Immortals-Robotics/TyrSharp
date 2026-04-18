using Tyr.Soccer.Knowledge;

namespace Tyr.Soccer.Plays;

public class Stop : IPlay
{
    public static bool IsApplicable() => Context.Referee.Stop();

    public Formation Tick()
    {
        var requiredRoles = new List<Role.IRole>();
        var desiredRoles = new List<Role.IRole>();

        requiredRoles.Add(new Role.Goalie());
        requiredRoles.Add(new Role.Defender(1));
        requiredRoles.Add(new Role.Defender(2));
        requiredRoles.Add(new Role.StopWall());

        var sortedZones = Context.Knowledge.SortedZonesByOffense;

        // Subtract required roles from our available robots
        var supportersToAdd = Math.Min(Context.Knowledge.OwnRobotsCount - requiredRoles.Count, sortedZones.Count);
        for (var i = 0; i < supportersToAdd; i++)
        {
            desiredRoles.Add(new Role.Supporter() { Zone = sortedZones.Dequeue() });
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
