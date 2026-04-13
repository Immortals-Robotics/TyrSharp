namespace Tyr.Soccer.Plays;

public class Halt : IPlay
{
    public static bool IsApplicable() => Context.Referee.Halt();

    public IReadOnlyList<Role.IRole> Tick()
    {
        throw new NotImplementedException();
    }
}