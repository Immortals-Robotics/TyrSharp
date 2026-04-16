using Tyr.Common.Time;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Role;

public record PenaltyWaiter(int Index, bool IsOurPenalty) : IRole
{
    public Tactics.ITactic CreateTactic(Robot.Robot robot)
    {
        return new Tactics.PenaltyWait(robot, Index, IsOurPenalty);
    }

    public float Importance => 10f;

    public DeltaTime CostFor(Robot.Robot robot)
    {
        var targetPos = Tactics.PenaltyWait.CalculateStaticPos(Index, IsOurPenalty);
        var trajectory = Navigation.Trajectory.TrajectoryBangBang.Make2D(robot.Position, robot.Velocity,
            targetPos, VelocityProfile.Mamooli);
        return DeltaTime.FromSeconds(trajectory.Duration);
    }
}
