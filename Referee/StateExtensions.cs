using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Gc;

namespace Tyr.Referee;

public static class StateExtensions
{
    public static TeamColor ToColor(this Command command)
    {
        switch (command)
        {
            case Command.PrepareKickoffBlue:
            case Command.PreparePenaltyBlue:
            case Command.DirectFreeBlue:
            case Command.BallPlacementBlue:
            case Command.TimeoutBlue:
            case Command.GoalBlue:
                return TeamColor.Blue;

            case Command.PrepareKickoffYellow:
            case Command.PreparePenaltyYellow:
            case Command.DirectFreeYellow:
            case Command.BallPlacementYellow:
            case Command.TimeoutYellow:
            case Command.GoalYellow:
                return TeamColor.Yellow;

            case Command.Halt:
            case Command.Stop:
            case Command.NormalStart:
            case Command.ForceStart:
                return TeamColor.Unknown;

            default:
                throw new ArgumentOutOfRangeException(nameof(command), command, null);
        }
    }
}