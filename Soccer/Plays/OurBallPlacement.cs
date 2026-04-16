using Tyr.Soccer.Role;

namespace Tyr.Soccer.Plays;

public class OurBallPlacement : IPlay
{
    private readonly IRole[] _roles =
    [
        new BallPlacer(1),
        new BallPlacer(2),
        new Goalie(),
        new Defender(1),
        new Defender(2),
    ];

    public static bool IsApplicable() => Context.Referee.OurBallPlacement();

    public IReadOnlyList<IRole> Tick() => _roles;
}
