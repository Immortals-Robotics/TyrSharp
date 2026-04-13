namespace Tyr.Soccer.Plays;

public class Stop : IPlay
{
    public static bool IsApplicable() => Context.Referee.Stop();

    public IReadOnlyList<Role.IRole> Tick()
    {
        throw new NotImplementedException();
    }
}