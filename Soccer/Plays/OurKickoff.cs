using System.Numerics;
using Tyr.Common.Math;
using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurKickoff : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurKickoff();

    public Formation Tick()
    {
        var ballPos = Context.Ball.State.Position;
        var side = -Context.SideSign; // Side we are attacking towards

        // Receiver positions based on C++ kickoffUsPass
        var mid5Pos = new Vector2(ballPos.X + side * 150f, Context.Field.Height - 300f);
        var mid1Pos = new Vector2(ballPos.X + side * 150f, -(Context.Field.Height - 300f));
        var mid2Pos = ballPos.PointOnConnectingLine(Context.Field.OwnGoal(), 1000f);

        var elapsed = Context.Referee.Elapsed(Context.Time);
        var canKick = Context.Referee.CanKickBall();
        // The attacker targets mid5 (the top receiver)
        var attackerTarget = mid5Pos;
        var shootPower = 0f;


        shootPower = canKick ? 3000f : 0f;


        Log.ZLogDebug($"elapsed: {elapsed}");

        return new Formation
        {
            RequiredRoles =
            [
                new Goalie(),
                new Defender(1),
                new Defender(2),
                new CircleBall()
                {
                    TargetPosition = attackerTarget,
                    CanKick = canKick,
                    ShootPower = shootPower
                },
            ],
            DesiredRoles =
            [
                new Waiter(mid5Pos),
                new Waiter(mid1Pos),
                new Waiter(mid2Pos),
            ]
        };
    }
}