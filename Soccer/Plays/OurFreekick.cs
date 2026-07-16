using Tyr.Common.Debug.Drawing;
using Tyr.Common.Time;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurFreekick : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurFreeKick();

    public Formation Tick()
    {
        var requiredRoles = new List<IRole>();
        var desiredRoles = new List<IRole>();
        requiredRoles.Add(new Goalie());
        requiredRoles.Add(new Defender(1));
        requiredRoles.Add(new Defender(2));

        var zones = Context.Knowledge.SortedZonesByOffense;
        var bestOffenseZone = zones.Count > 0 ? zones.Peek() : null;
        if (bestOffenseZone != null) Draw.DrawCircle(bestOffenseZone.BestPosOffence, 200, Color.Amber, Options.Outline());
        var chipperTarget = bestOffenseZone?.BestPosOffence ?? Context.Field.OppGoal();
        var chipPower = 0;

        if (Context.Referee.Gc.CurrentActionTimeRemaining < DeltaTime.FromSeconds(2))
        {
            chipPower = 50;
        }

        Log.ZLogDebug($"chip: {Context.Referee.Gc.CurrentActionTimeRemaining} -> {chipPower}");
        requiredRoles.Add(new CircleBall
        {
            TargetPosition = chipperTarget,
            CanKick = Context.Referee.CanKickBall(),
            ShootPower = 0,
            ChipPower = chipPower
        });

        // Supporters
        while (requiredRoles.Count + desiredRoles.Count < Context.OwnRobots.Count)
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