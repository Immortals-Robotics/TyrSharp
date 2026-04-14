namespace Tyr.Soccer.Plays;

public class NormalPlay : IPlay
{
    public static bool IsApplicable() => Context.Referee.Running();

    public IReadOnlyList<Role.IRole> Tick()
    {
        return [new Role.Attacker()];
    }
}
