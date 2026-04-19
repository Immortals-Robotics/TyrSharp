using Tyr.Common.Time;
using Tyr.Soccer.Tactics;

namespace Tyr.Soccer.Role;

public interface IRole
{
    ITactic CreateTactic(Robot.Robot robot);

    bool IsGoalie => false;
    float Importance { get; }
    DeltaTime StickinessBonus => DeltaTime.FromMilliseconds(50);
    DeltaTime CostFor(Robot.Robot robot);
}
