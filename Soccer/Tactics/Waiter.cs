using System.Numerics;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class Waiter(Robot.Robot robot, Vector2 target, Vector2? lookAt = null) : ITactic
{
    public Robot.Robot Robot => robot;

    public ISkill Tick() => new GoToPoint()
    {
        Target = target,
        LookAt = lookAt ?? Context.Ball.State.Position
    };
}
