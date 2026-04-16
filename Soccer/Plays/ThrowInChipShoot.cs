using System.Numerics;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class ThrowInChipShoot : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurFreeKick();

    public IReadOnlyList<IRole> Tick()
    {
        var roles = new List<IRole>();
        roles.Add(new Goalie());
        roles.Add(new Defender(1));
        roles.Add(new Defender(2));

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
        roles.Add(new CircleBall 
        { 
            TargetPosition = chipperTarget, 
            CanKick = Context.Referee.CanKickBall(), 
            ShootPower = 0f, 
            ChipPower = chipPower 
        });

        // Receiver
        var randomParam = (float)((Context.Referee.Timestamp.Nanoseconds % 1000000) / 1000000f);
        roles.Add(new ThrowInReceiver(randomParam));

        // Supporters
        var zones = Context.Knowledge.Zones.OrderByDescending(z => z.ScoreOffense).ToList();
        int zoneIdx = 0;
        while (roles.Count < Context.OwnRobots.Count)
        {
            if (zoneIdx < zones.Count)
                roles.Add(new Supporter { Zone = zones[zoneIdx++] });
            else
                break;
        }

        return roles;
    }
}
