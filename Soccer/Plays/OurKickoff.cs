using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurKickoff : IPlay
{
    public static bool IsApplicable() => Context.Referee.OurKickoff();

    public IReadOnlyList<IRole> Tick()
    {
        return
        [
            new CircleBall()
            {
                TargetPosition = Context.Field.OppGoal(),
                CanKick = Context.Referee.CanKickBall(),
            },
        ];
    }
}
