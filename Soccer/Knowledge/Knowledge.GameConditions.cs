using System.Numerics;
using Tyr.Common.Math;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    private void UpdateGameConditions()
    {
        GoalieDiveAllowed = Context.Referee.Running() || Context.Referee.TheirPenaltyKick();
    }
}