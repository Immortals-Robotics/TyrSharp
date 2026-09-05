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
        var zones = Context.Knowledge.SortedZonesByOffense;
        while (requiredRoles.Count + desiredRoles.Count < Context.Knowledge.OwnRobotsCount)
        {
            if (zones.Count == 0)
            {
                break;
            }

            desiredRoles.Add(new Supporter { Zone = zones.Dequeue() });
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
