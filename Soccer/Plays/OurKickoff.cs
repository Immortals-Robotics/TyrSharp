using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurKickoff : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurKickoff();

    public Formation Tick()
    {
        return new Formation
        {
            RequiredRoles =
            [
                new CircleBall()
                {
                    TargetPosition = Context.Field.OppGoal(),
                    CanKick = Context.Referee.CanKickBall(),
                },
            ]
        };
    }
}
