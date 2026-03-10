namespace Tyr.Soccer.Plays;

public class Stop : IPlay
{
    public static bool IsApplicable() => Context.Referee.Stop();

    public IReadOnlyList<Role.Role> Tick()
    {
        throw new NotImplementedException();
    }
}