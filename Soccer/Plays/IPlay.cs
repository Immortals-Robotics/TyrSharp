namespace Tyr.Soccer.Plays;

public interface IPlay
{
    public static abstract bool IsApplicable();
    public IReadOnlyList<Role.Role> Tick();
}
