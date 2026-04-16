using System.Numerics;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

public class WaitForBall(Robot.Robot robot, Vector2 target) : ITactic
{
    public Robot.Robot Robot => robot;
    public Vector2 Target => target;

    public ISkill Tick() => new Skills.WaitForBall() { StaticPosition = Target };
}
