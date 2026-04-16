namespace Tyr.Soccer.Plays;

public class NormalPlay : IPlay
{
    public static bool IsApplicable() => Context.Referee.Running();

    public Formation Tick()
    {
        return new Formation
        {
            RequiredRoles =
            [
                new Role.Goalie(),
                new Role.Defender(1),
                new Role.Defender(2),
                new Role.Attacker()
            ]
        };
    }
}
