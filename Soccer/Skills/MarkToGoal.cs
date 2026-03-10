using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

[Configurable]
public partial class MarkToGoal : ISkill
{
    [ConfigEntry] private static DeltaTime OppPredictTime { get; set; } = DeltaTime.FromMilliseconds(150f);
    [ConfigEntry] private static bool PenaltyAreaMark { get; set; } = false;
    [ConfigEntry] private static float PenaltyAreaMarkDistance { get; set; } = 200f;

    public required FilteredRobot Opponent { get; set; }
    public float Distance { get; set; } = 220f;

    public void Execute(Robot.Robot robot)
    {
        var predictedOpp = Opponent.State.Position + Opponent.State.Velocity * (float)OppPredictTime.Seconds;
        var ownGoal      = Context.Field.OwnGoal();

        Vector2 target;

        if (PenaltyAreaMark)
        {
            var penaltyAreaDist      = PenaltyAreaMarkDistance;
            var penaltyAreaHalfWidth = Context.Field.PenaltyAreaWidth!.Value / 2f;
            var start = new Vector2(ownGoal.X, -(penaltyAreaHalfWidth + penaltyAreaDist));
            var w     = -Context.SideSign * (penaltyAreaDist + Context.Field.PenaltyAreaDepth!.Value);
            var h     = Context.Field.PenaltyAreaWidth!.Value + 2 * penaltyAreaDist;

            var virtualDefenseArea = Rectangle.FromCornerAndSize(start, w, h);
            var shotLine           = Line.FromTwoPoints(ownGoal, predictedOpp);
            var (i0, i1)           = Geometry.Intersection(virtualDefenseArea, shotLine);

            if (i0.HasValue && i1.HasValue)
            {
                target = Vector2.Distance(i0.Value, ownGoal) > Vector2.Distance(i1.Value, ownGoal)
                    ? i0.Value
                    : i1.Value;
            }
            else
            {
                target = predictedOpp.PointOnConnectingLine(ownGoal, Distance);
            }
        }
        else
        {
            target = predictedOpp.PointOnConnectingLine(ownGoal, Distance);
        }

        robot.Face(Context.Ball.State.Position);
        robot.Navigate(target, VelocityProfile.Mamooli, NavigationFlags.BallObstacle);
    }
}