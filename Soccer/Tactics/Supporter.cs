using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class Supporter(Robot.Robot robot) : ITactic
{
    public required Zone Zone { get; init; }

    public Robot.Robot Robot => robot;

    public ISkill Tick() => new GoToPoint()
    {
        Target = Zone.BestPosOffence,
        LookAt = Context.Ball.State.Position
    };
}
