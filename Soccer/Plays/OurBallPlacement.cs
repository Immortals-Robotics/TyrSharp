using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurBallPlacement : IPlay
{
    private readonly IRole[] _requiredRoles =
    [
        new BallPlacer(1),
        new BallPlacer(2),
        new Goalie(),
        new Defender(1),
        new Defender(2),
    ];


    public static bool IsApplicable() => Context.Referee.OurBallPlacement();

    public Formation Tick()
    {
        var desiredRoles = new List<IRole>();

        // The rest are supporters in offensive zones
        var zones = Context.Knowledge.SortedZonesByOffense;
        while (_requiredRoles.Count() + desiredRoles.Count < Context.Knowledge.OwnRobotsCount)
        {
            if (zones.Count == 0)
            {
                break;
            }

            desiredRoles.Add(new Supporter { Zone = zones.Dequeue() });
        }

        return new Formation
        {
            RequiredRoles = _requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}