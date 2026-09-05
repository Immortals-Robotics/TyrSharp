using Tyr.Common.Config;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public partial class MarkToBall : ISkill
{
    [ConfigEntry] private static partial DeltaTime OppPredictTime { get; set; } = DeltaTime.FromMilliseconds(150f);
    
    public required FilteredRobot Opponent { get; set; }
    public float Distance { get; set; }
    
    public void Execute(Robot.Robot robot)
    {
        var predictedOpp = Opponent.State.Position + Opponent.State.Velocity * (float)OppPredictTime.Seconds;
        var target       = predictedOpp.PointOnConnectingLine(Context.Ball.State.Position, Distance);

        robot.Face(Context.Ball.State.Position);
        robot.Navigate(target, VelocityProfile.Mamooli, NavigationFlags.BallObstacle);
    }
}
