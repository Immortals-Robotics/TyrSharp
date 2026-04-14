using Tyr.Soccer.Tactics;

namespace Tyr.Soccer.Role;

public interface IRole
{
    ITactic CreateTactic(Robot.Robot robot);

    float Importance { get; }
    float CostFor(Robot.Robot robot);
}
