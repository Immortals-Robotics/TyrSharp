using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics;

[Configurable]
public partial class Mark : ITactic
{
    [ConfigEntry] private static float Distance { get; set; } = 220f;

    public required Robot.Robot Robot { get; init; } = null!;

    public required FilteredRobot Opponent { get; init; }

    public ISkill Tick()
    {
        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - Opponent.State.Position);
        var oppToGoal = Vector2.Normalize(Context.Field.OwnGoal() - Opponent.State.Position);

        var oppToGoalDis = Vector2.Distance(Opponent.State.Position, Context.Field.OwnGoal());
        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        if (oneTouchDot > 0 || oppToGoalDis < 2500)
        {
            return new MarkToGoal { Opponent = Opponent, Distance = Distance };
        }
        else
        {
            return new MarkToBall  { Opponent = Opponent, Distance = Distance };
        }

    }
}
