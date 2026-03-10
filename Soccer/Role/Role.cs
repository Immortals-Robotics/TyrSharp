using Tyr.Common.Vision.Data;
using Tyr.Soccer.Tactics;

namespace Tyr.Soccer.Role;

public class Role
{
    public required ITactic Tactic { get; set; }

    public Priority Priority { get; set; }
    public required Func<RobotState, float> CostFn { get; set; }

    public Robot.Robot? Robot { get; set; }
}