using Tyr.Common.Time;
using Tyr.Soccer.Tactics;

namespace Tyr.Soccer.Role;

public interface IRole
{
    ITactic CreateTactic(Robot.Robot robot);

    float Importance { get; }
    DeltaTime CostFor(Robot.Robot robot);
}
