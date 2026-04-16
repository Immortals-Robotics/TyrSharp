using System.Numerics;
using Tyr.Common.Vision.Data;

namespace Tyr.Soccer.Knowledge;

public partial class Knowledge
{
    public List<(FilteredRobot Robot, float Threat)> OpponentThreats { get; } = [];

    private void UpdateOpponentThreats()
    {
        OpponentThreats.Clear();

        foreach (var opp in Context.OppRobots)
        {
            var threat = CalculateOpponentThreat(opp);
            if (threat >= 0)
                OpponentThreats.Add((opp, threat));
        }

        OpponentThreats.Sort((a, b) => b.Threat.CompareTo(a.Threat));
    }

    public static float CalculateOpponentThreat(FilteredRobot robot)
    {
        if (robot.Quality <= 0)
            return -1;

        if (robot.Id.Id == (int)Context.Referee.OppInfo().Goalkeeper)
            return -1;

        if (Vector2.Distance(robot.State.Position, Context.Ball.State.Position) < 500)
            return -1;

        if (robot.State.Position.X * Context.SideSign < 1000)
            return -1;

        var oppDisToGoal = Vector2.Distance(robot.State.Position, Context.Field.OwnGoal());

        var t1Angle = robot.State.Position.AngleWith(Context.Field.OwnGoalPostBottom());
        var t2Angle = robot.State.Position.AngleWith(Context.Field.OwnGoalPostTop());

        var oppOpenAngleToGoal = MathF.Abs((t2Angle - t1Angle).DegNormalized);

        var oppToBall = Vector2.Normalize(Context.Ball.State.Position - robot.State.Position);
        var oppToGoal = Vector2.Normalize(Context.Field.OwnGoal() - robot.State.Position);

        var oneTouchDot = Vector2.Dot(oppToBall, oppToGoal);

        var ballToOppDis = Vector2.Distance(Context.Ball.State.Position, robot.State.Position);

        float scoreGoalDis;
        if (oppDisToGoal < 3000)
            scoreGoalDis = 1.0f;
        else
            scoreGoalDis = 1.0f - MathF.Pow(MathF.Max(0.0f, (oppDisToGoal - 3000.0f) / 3000.0f), 0.5f);

        float scoreBallDis;
        if (ballToOppDis < 2000)
            scoreBallDis = MathF.Pow(ballToOppDis / 2000.0f, 2.0f);
        else if (ballToOppDis < 6000)
            scoreBallDis = 1.0f;
        else
            scoreBallDis = 1.0f - (ballToOppDis - 6000.0f) / 6000.0f;

        var scoreOpenAngle = oppOpenAngleToGoal / 15.0f;

        float scoreOneTouchAngle;
        if (oneTouchDot >= 0.0f)
            scoreOneTouchAngle = 4 * oneTouchDot - 4 * MathF.Pow(oneTouchDot, 2.0f);
        else
            scoreOneTouchAngle = 0.0f;

        scoreGoalDis = Math.Clamp(scoreGoalDis, 0.0f, 1.0f);
        scoreBallDis = Math.Clamp(scoreBallDis, 0.0f, 1.0f);
        scoreOpenAngle = Math.Clamp(scoreOpenAngle, 0.0f, 1.0f);
        scoreOneTouchAngle = Math.Clamp(scoreOneTouchAngle, 0.0f, 1.0f);

        var finalScoreOneTouch = scoreOneTouchAngle * MathF.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);
        var finalScoreTurnShoot = MathF.Min(scoreBallDis * scoreGoalDis, scoreOpenAngle);

        return MathF.Max(finalScoreOneTouch, finalScoreTurnShoot);
    }

    public float CalculateMarkCost(int robotId, int oppId)
    {
        var own = Context.OwnRobots[robotId];
        var oppIndex = Context.OppRobots.FindIndex(r => r.Id.Id == oppId);

        if (!own.Seen || oppIndex == -1)
            return -1;

        var opp = Context.OppRobots[oppIndex];
        if (opp.Quality <= 0)
            return -1;

        const float kPredictT = 0.3f;

        var predictedPosOwn = own.State.Position + own.State.Velocity * kPredictT;
        var predictedPosOpp = opp.State.Position + opp.State.Velocity * kPredictT;
        var disPred = Vector2.Distance(predictedPosOwn, predictedPosOpp);

        var alreadyMarked = MarkMap.TryGetValue(robotId, out var currentOpp) && currentOpp == oppId;

        var costStay = alreadyMarked ? (disPred - 500.0f) / 1000.0f : 1.0f;
        var costReach = (disPred - 500.0f) / 10000.0f;

        costStay = Math.Clamp(costStay, 0.0f, 1.0f);
        costReach = Math.Clamp(costReach, 0.0f, 1.0f);

        const float kWeightStay = 0.0f;
        const float kWeightReach = 1.0f;

        return kWeightStay * costStay + kWeightReach * costReach;
    }
}
