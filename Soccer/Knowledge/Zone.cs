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

    public Vector2 BestPosDefence { get; private set; }
    public Vector2 BestPosOffence { get; private set; }

    public double ScoreDefense { get; private set; }
    public double ScoreOffense { get; private set; }


    public void UpdateScore()
    {
        BestPosDefence = Rect.Center;
        BestPosOffence = Rect.Center;

        ScoreDefense = -1f;
        ScoreOffense = -1f;

        if (Context.Field.OwnPenaltyArea().Inside(Rect.Center) || Context.Field.OppPenaltyArea().Inside(Rect.Center))
        {
            return;
        }

        if (Context.Referee.GameState == GameState.Kickoff && Rect.Center.X < 0f)
        {
            return;
        }

        if (Rect.Inside(Context.Ball.State.Position))
        {
            ScoreDefense = 0f;
            ScoreOffense = 0f;
            return;
        }

        var ourRobotsInside = 0u;
        foreach (var robot in Context.OwnRobots.Where(robot => Rect.Inside(robot.State.Position)))
        {
            ourRobotsInside++;
        }

        if (ourRobotsInside > 2)
        {
            ScoreDefense = 0;
            ScoreOffense = 0;
            return;
        }


        ScoreDefense = CalculateScoreDefense(BestPosDefence);
        ScoreOffense = CalculateScoreOffense(BestPosOffence);
    }

    public void DrawZone()
    {
        var zoneColor = ScoreOffense > ScoreDefense
            ? GetZoneScoreColor(ScoreOffense, OffensePalette)
            : GetZoneScoreColor(ScoreDefense, DefensePalette);
        Draw.DrawRectangle(Rect, zoneColor, Options.Filled);
    }

    private static float CalculateScoreDefense(Vector2 point)
    {
        var oppDisToGoal = Vector2.Distance(point, Context.Field.OwnGoal());

        var post1Angle = point.AngleWith(Context.Field.OwnGoalPostBottom());
        var post2Angle = point.AngleWith(Context.Field.OwnGoalPostTop());

        var oppOpenAngleToGoal = Math.Abs((post2Angle - post1Angle).Deg);

        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - point);
        var oppToGoal = Vector2.Normalize(Context.Field.OwnGoal() - point);

        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        float scoreGoalDis;
        if (oppDisToGoal < 3000f)
            scoreGoalDis = 1f;
        else
            scoreGoalDis = 1f - MathF.Pow(Math.Max(0f, (oppDisToGoal - 3000f) / 3000f), 0.5f);

        var ballToOppDis = Vector2.Distance(Context.Ball.State.Position, point);

        if (ballToOppDis < 400f)
        {
            return -1f;
        }

        var scoreBallDis = ballToOppDis switch
        {
            < 2000 => MathF.Pow(ballToOppDis / 2000f, 2f),
            < 6000 => 1f,
            _ => 1f - (ballToOppDis - 6000f) / 6000f
        };

        var scoreOpenAngle = oppOpenAngleToGoal / 15f;

        float scoreOneTouchAngle;

        if (oneTouchDot >= 0f)
            scoreOneTouchAngle = 4f * oneTouchDot - 4f * MathF.Pow(oneTouchDot, 2f);
        else
            scoreOneTouchAngle = 0f;

        scoreGoalDis = Math.Clamp(scoreGoalDis, 0f, 1f);
        scoreBallDis = Math.Clamp(scoreBallDis, 0f, 1f);
        scoreOpenAngle = Math.Clamp(scoreOpenAngle, 0f, 1f);
        scoreOneTouchAngle = Math.Clamp(scoreOneTouchAngle, 0f, 1f);

        var finalScoreOneTouch =
            scoreOneTouchAngle * Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);
        var finalScoreTurnShoot = Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);

        return Math.Max(finalScoreOneTouch, finalScoreTurnShoot);
    }

    private static float CalculateScoreOffense(Vector2 point)
    {
        var posBallLine = Line.FromTwoPoints(Context.Ball.State.Position, point);
        var goalLine = Line.FromSegment(Context.Field.OppGoalLine());
        var goalIntersection = Geometry.Intersection(posBallLine, goalLine);

        var dot = Vector2.Dot(Vector2.Normalize(point - Context.Ball.State.Position),
            Vector2.Normalize(Context.Field.OppGoal() - Context.Ball.State.Position));

        if (dot > 0 && goalIntersection.HasValue && Math.Abs(goalIntersection.Value.Y) < Context.Field.GoalWidth / 2.0f)
        {
            return 0f;
        }

        var oppDisToGoal = Vector2.Distance(point, Context.Field.OppGoal());

        var post1Angle = point.AngleWith(Context.Field.OppGoalPostBottom());
        var post2Angle = point.AngleWith(Context.Field.OppGoalPostTop());

        var oppOpenAngleToGoal = Math.Abs((post2Angle - post1Angle).Deg);

        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - point);
        var oppToGoal = Vector2.Normalize(Context.Field.OppGoal() - point);

        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        float scoreGoalDis;
        if (oppDisToGoal < 3000)
            scoreGoalDis = 1f;
        else
            scoreGoalDis = 1f - MathF.Pow(Math.Max(0f, (oppDisToGoal - 3000f) / 3000f), 0f);

        var ballToOppDis = Vector2.Distance(Context.Ball.State.Position, point);

        var scoreBallDis = ballToOppDis switch
        {
            < 2000f => MathF.Pow(ballToOppDis / 2000f, 2f),
            < 6000f => 1f,
            _ => 1f - (ballToOppDis - 6000f) / 6000f
        };

        var scoreOpenAngle = oppOpenAngleToGoal / 15f;

        float scoreOneTouchAngle;

        if (oneTouchDot >= 0f)
            scoreOneTouchAngle = 4f * oneTouchDot - 4f * MathF.Pow(oneTouchDot, 2f);
        else
            scoreOneTouchAngle = 0f;

        scoreGoalDis = Math.Clamp(scoreGoalDis, 0f, 1f);
        scoreBallDis = Math.Clamp(scoreBallDis, 0f, 1f);
        scoreOpenAngle = Math.Clamp(scoreOpenAngle, 0f, 1f);
        scoreOneTouchAngle = Math.Clamp(scoreOneTouchAngle, 0f, 1f);

        var passAngleOk = Vector2.Dot(
            Vector2.Normalize(point - Context.Ball.State.Position),
            Vector2.Normalize(Context.Field.OwnGoal() - Context.Ball.State.Position)) < 0.85f;
        var ownGoalAngleScore = passAngleOk ? 1f : 0f;

        var finalScoreOneTouch = ownGoalAngleScore * scoreOneTouchAngle *
                                 Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);
        var finalScoreTurnShoot = Math.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);


        return Math.Max(finalScoreOneTouch, finalScoreTurnShoot);
    }

    private static readonly Color[] DefensePalette =
    [
        Color.Blue500,
        Color.Cyan500,
        Color.Teal500,
    ];

    private static readonly Color[] OffensePalette =
    [
        Color.Yellow500,
        Color.Orange500,
        Color.Red500,
    ];

    private static Color GetZoneScoreColor(double score, Color[] palette)
    {
        var clampedScore = Math.Clamp(score, 0f, 1f);
        var scaledScore = clampedScore * (palette.Length - 1);
        var lowerIndex = (int)Math.Floor(scaledScore);
        var upperIndex = Math.Min(lowerIndex + 1, palette.Length - 1);
        var blend = (float)(scaledScore - lowerIndex);
        var baseColor = BlendColor(palette[lowerIndex], palette[upperIndex], blend);

        return baseColor.WithAlpha((float)(0.5 * clampedScore));
    }

    private static Color BlendColor(Color start, Color end, float blend) => new(
        start.R + ((end.R - start.R) * blend),
        start.G + ((end.G - start.G) * blend),
        start.B + ((end.B - start.B) * blend));
}
