using System.Numerics;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class ThrowInChipShoot : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurFreeKick();

    public Formation Tick()
    {
        var requiredRoles = new List<IRole>();
        var desiredRoles = new List<IRole>();
        requiredRoles.Add(new Goalie());
        requiredRoles.Add(new Defender(1));
        requiredRoles.Add(new Defender(2));

        var elapsed = Context.Referee.Elapsed(Context.Time);
        var ballPos = Context.Ball.State.Position;
        
        // Chipper
        Vector2 chipperTarget;
        float chipPower = 0f;
        if (elapsed.Seconds > 4f)
        {
            chipperTarget = new Vector2(Context.Field.OppGoal().X, MathF.Sign(ballPos.Y) * 200.0f);
            chipPower = 1500f;
        }
        else
        {
            chipperTarget = Context.Field.OppGoal();
        }
        requiredRoles.Add(new CircleBall 
        { 
            TargetPosition = chipperTarget, 
            CanKick = Context.Referee.CanKickBall(), 
            ShootPower = 0f, 
            ChipPower = chipPower 
        });

        // Receiver
        var randomParam = (float)((Context.Referee.Timestamp.Nanoseconds % 1000000) / 1000000f);
        requiredRoles.Add(new ThrowInReceiver(randomParam));

        // Supporters
        var zones = Context.Knowledge.Zones.OrderByDescending(z => z.ScoreOffense).ToList();
        int zoneIdx = 0;
        while (requiredRoles.Count + desiredRoles.Count < Context.OwnRobots.Count)
        {
            if (zoneIdx < zones.Count)
                desiredRoles.Add(new Supporter { Zone = zones[zoneIdx++] });
            else
                break;
        }

        return new Formation
        {
            RequiredRoles = requiredRoles,
            DesiredRoles = desiredRoles
        };
    }
}
