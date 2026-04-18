using System.Numerics;
using Tyr.Common.Math;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class TheirKickoff : IPlay
{
    private const float ThreatLaneMaxAbsX = 1500f;
    private const float ThreatLaneMinAbsY = 600f;

    public static bool IsApplicable() => Context.Referee.TheirKickoff();

    public Formation Tick()
    {
        var requiredRoles = new List<IRole>
        {
            new Goalie(),
            new Defender(1),
            new Defender(2),
            new DefenceWall(),
        };

        var desiredRoles = new List<IRole>();

        // Ported from Tyr kickoff_their_one_wall:
        // pick one threat in each side lane and block the passing lane to our goal.
        Vector2? negLaneTarget = null;
        Vector2? posLaneTarget = null;

        foreach (var opp in Context.OppRobots)
        {
            if (opp.Quality <= 0f)
                continue;

            var oppPos = opp.State.Position;
            if (MathF.Abs(oppPos.X) >= ThreatLaneMaxAbsX || MathF.Abs(oppPos.Y) <= ThreatLaneMinAbsY)
                continue;

            var laneTarget = CalculateLaneBlockTarget(oppPos);
            if (oppPos.Y < 0f)
            {
                negLaneTarget = laneTarget;
            }
            else if (oppPos.Y > 0f)
            {
                posLaneTarget = laneTarget;
            }
        }

        if (negLaneTarget.HasValue)
        {
            desiredRoles.Add(new Waiter(negLaneTarget.Value, Context.Field.OppGoal()));
        }

        if (posLaneTarget.HasValue)
        {
            desiredRoles.Add(new Waiter(posLaneTarget.Value, Context.Field.OppGoal()));
        }

        var zones = Context.Knowledge.SortedZonesByDefense;
        while (requiredRoles.Count + desiredRoles.Count < Context.OwnRobots.Count)
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

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }

    private static Vector2 CalculateLaneBlockTarget(Vector2 opponentPosition)
    {
        var distance = (MathF.Abs(opponentPosition.X) + 140f) * 2.5f;
        return opponentPosition.PointOnConnectingLine(Context.Field.OwnGoal(), distance);
    }
}
