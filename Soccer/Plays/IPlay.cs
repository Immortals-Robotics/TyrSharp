namespace Tyr.Soccer.Plays;

public interface IPlay
{
    public static abstract bool IsApplicable();
    public Formation Tick();
}
