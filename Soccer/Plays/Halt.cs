namespace Tyr.Soccer.Plays;

public class Halt : IPlay
{
    public static bool IsApplicable() => Context.Referee.Halt();

    public Formation Tick() => Formation.Empty;
}
