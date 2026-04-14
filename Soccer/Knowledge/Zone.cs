using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Referee.Data;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Knowledge;

[Configurable]
public partial class Zone
{
    [ConfigEntry] public static int ZoneCountX { get; set; } = 6;
    [ConfigEntry] public static int ZoneCountY { get; set; } = 4;

    public Rectangle Rect { get; init; }
    public Vector2 BestPos { get; private set; }
    public double Score { get; private set; }

    public void UpdateScore(bool isDefending)
    {
        BestPos = Rect.Center;
        Score = -1f;

        if (Context.Field.OwnPenaltyArea().Inside(Rect.Center) || Context.Field.OppPenaltyArea().Inside(Rect.Center))
        {
            return;
        }

        if (Rect.Inside(Context.Ball.State.Position))
        {
            Score = 0f;
            return;
        }


        if (Context.Referee.GameState == GameState.Kickoff && Rect.Center.X < 0f)
        {
            return;
        }

        var ourRobotsInside = 0u;
        foreach (var robot in Context.OwnRobots.Where(robot => Rect.Inside(robot.State.Position)))
        {
            ourRobotsInside++;
        }

        if (ourRobotsInside > 2)
        {
            Score = 0;
            return;
        }

        if (isDefending)
            ScoreDefense();
        else
            ScoreOffense();
    }

    public void DrawZone()
    {
        var zoneColor = GetZoneScoreColor(Score);
        Draw.DrawRectangle(Rect, zoneColor, Options.Filled);
    }

    void ScoreDefense()
    {
        var oppDisToGoal = Vector2.Distance(BestPos, Context.Field.OwnGoal());

        var post1Angle = BestPos.AngleWith(Context.Field.OwnGoalPostBottom());
        var post2Angle = BestPos.AngleWith(Context.Field.OwnGoalPostTop());

        var oppOpenAngleToGoal = Math.Abs((post2Angle - post1Angle).Deg);

        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - BestPos);
        var oppToGoal = Vector2.Normalize(Context.Field.OwnGoal() - BestPos);

        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        var scoreGoalDis = 0.0d;
        if (oppDisToGoal < 3000)
            scoreGoalDis = 1.0d;
        else
            scoreGoalDis = 1.0d - Math.Pow(Math.Max(0.0d, (oppDisToGoal - 3000.0d) / 3000.0d), 0.5d);

        var ballToOppDis = Vector2.Distance(Context.Ball.State.Position, BestPos);

        if (ballToOppDis < 400)
        {
            Score = -1.0d;
            return;
        }

        var scoreBallDis = ballToOppDis switch
        {
            < 2000 => Math.Pow(ballToOppDis / 2000.0d, 2.0d),
            < 6000 => 1.0d,
            _ => 1.0d - (ballToOppDis - 6000.0d) / 6000.0d
        };

        var scoreOpenAngle = oppOpenAngleToGoal / 15.0d;

        var scoreOneTouchAngle = 0.0d;

        if (oneTouchDot >= 0.0d)
            scoreOneTouchAngle = 4 * oneTouchDot - 4 * Math.Pow(oneTouchDot, 2.0d);
        else
            scoreOneTouchAngle = 0.0d;

        scoreGoalDis = Math.Clamp(scoreGoalDis, 0.0d, 1.0d);
        scoreBallDis = Math.Clamp(scoreBallDis, 0.0d, 1.0d);
        scoreOpenAngle = Math.Clamp(scoreOpenAngle, 0.0d, 1.0d);
        scoreOneTouchAngle = Math.Clamp(scoreOneTouchAngle, 0.0d, 1.0d);

        var finalScoreOneTouch =
            scoreOneTouchAngle * Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);
        var finalScoreTurnShoot = Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);

        Score = Math.Max(finalScoreOneTouch, finalScoreTurnShoot);
    }

    void ScoreOffense()
    {
        var posBallLine = Line.FromTwoPoints(Context.Ball.State.Position, BestPos);
        var goalLine = Line.FromSegment(Context.Field.OppGoalLine());
        var goalIntersection = Geometry.Intersection(posBallLine, goalLine);

        var dot = Vector2.Dot(Vector2.Normalize(BestPos - Context.Ball.State.Position),
            Vector2.Normalize(Context.Field.OppGoal() - Context.Ball.State.Position));

        if (dot > 0 && goalIntersection.HasValue && Math.Abs(goalIntersection.Value.Y) < Context.Field.GoalWidth / 2.0f)
        {
            Score = 0;
            return;
        }

        var oppDisToGoal = Vector2.Distance(BestPos, Context.Field.OppGoal());

        var post1Angle = BestPos.AngleWith(Context.Field.OppGoalPostBottom());
        var post2Angle = BestPos.AngleWith(Context.Field.OppGoalPostTop());

        var oppOpenAngleToGoal = Math.Abs((post2Angle - post1Angle).Deg);

        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - BestPos);
        var oppToGoal = Vector2.Normalize(Context.Field.OppGoal() - BestPos);

        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        var scoreGoalDis = 0.0d;
        if (oppDisToGoal < 3000)
            scoreGoalDis = 1.0d;
        else
            scoreGoalDis = 1.0d - Math.Pow(Math.Max(0.0d, (oppDisToGoal - 3000.0d) / 3000.0d), 0.5d);

        var ballToOppDis = Vector2.Distance(Context.Ball.State.Position, BestPos);

        var scoreBallDis = ballToOppDis switch
        {
            < 2000 => Math.Pow(ballToOppDis / 2000.0d, 2.0d),
            < 6000 => 1.0d,
            _ => 1.0d - (ballToOppDis - 6000.0d) / 6000.0d
        };

        var scoreOpenAngle = oppOpenAngleToGoal / 15.0d;

        var scoreOneTouchAngle = 0.0d;

        if (oneTouchDot >= 0.0d)
            scoreOneTouchAngle = 4 * oneTouchDot - 4 * Math.Pow(oneTouchDot, 2.0d);
        else
            scoreOneTouchAngle = 0.0d;

        scoreGoalDis = Math.Clamp(scoreGoalDis, 0.0d, 1.0d);
        scoreBallDis = Math.Clamp(scoreBallDis, 0.0d, 1.0d);
        scoreOpenAngle = Math.Clamp(scoreOpenAngle, 0.0d, 1.0d);
        scoreOneTouchAngle = Math.Clamp(scoreOneTouchAngle, 0.0d, 1.0d);

        var passAngleOk = Vector2.Dot(
            Vector2.Normalize(BestPos - Context.Ball.State.Position),
            Vector2.Normalize(Context.Field.OwnGoal() - Context.Ball.State.Position)) < 0.85d;
        var ownGoalAngleScore = passAngleOk ? 1.0d : 0.0d;

        var finalScoreOneTouch = ownGoalAngleScore * scoreOneTouchAngle *
                                 Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);
        var finalScoreTurnShoot = Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);


        Score = Math.Max(finalScoreOneTouch, finalScoreTurnShoot);
    }

    private static readonly Color[] ZoneScorePalette =
    [
        Color.Blue500,
        Color.Cyan500,
        Color.Lime500,
        Color.Yellow500,
        Color.Orange500,
        Color.Red500,
    ];

    private static Color GetZoneScoreColor(double score)
    {
        var clampedScore = Math.Clamp(score, 0f, 1f);
        var scaledScore = clampedScore * (ZoneScorePalette.Length - 1);
        var lowerIndex = (int)Math.Floor(scaledScore);
        var upperIndex = Math.Min(lowerIndex + 1, ZoneScorePalette.Length - 1);
        var blend = (float)(scaledScore - lowerIndex);
        var baseColor = BlendColor(ZoneScorePalette[lowerIndex], ZoneScorePalette[upperIndex], blend);

        return baseColor.WithAlpha((float)(0.5 * clampedScore));
    }

    private static Color BlendColor(Color start, Color end, float blend) => new(
        start.R + ((end.R - start.R) * blend),
        start.G + ((end.G - start.G) * blend),
        start.B + ((end.B - start.B) * blend));
}
