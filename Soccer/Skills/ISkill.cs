using System.Numerics;
using Tyr.Common;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Skills;

public interface ISkill
{
    public void Execute(Robot.Robot robot);
}

internal readonly record struct OpenAngleSolution(Angle Center, Angle Magnitude);

internal static class SkillMath
{
    private const int MaxObstacleArcs = 64;
    private const int OpenAngleSamples = 100;

    public static float SmoothStep01(float value)
    {
        var t = Math.Clamp(value, 0f, 1f);
        return t * t * (3f - 2f * t);
    }

    public static BallState PredictBall(DeltaTime timeAhead)
    {
        return ServiceLocator.BallTrajectoryFactory.GetStateAt(Context.Ball.State, timeAhead);
    }

    public static float CalculateBallRobotReachTime(Robot.Robot robot, Angle angle, VelocityProfile profile, float waitTimeSeconds)
    {
        const float maxTime = 5f;

        for (var t = 0f; t < maxTime; t += 0.1f)
        {
            var ballState = PredictBall(DeltaTime.FromSeconds(t));
            var interceptionPoint = ballState.Position.CircleAroundPoint(angle, Context.RobotRadius);
            var reachTime = robot.CalculateReachTime(interceptionPoint, profile);

            if (reachTime + waitTimeSeconds <= t)
            {
                return reachTime;
            }
        }

        return maxTime;
    }

    public static Angle CalculateSteeringHeading(Angle toBallAngle, float angleToTarget)
    {
        const float maxSteeringAngle = 15f;
        var angleOffset = MathF.Abs(angleToTarget) > maxSteeringAngle
            ? MathF.CopySign(maxSteeringAngle, angleToTarget)
            : angleToTarget;

        return toBallAngle + Angle.FromDeg(angleOffset);
    }

    public static Vector2 CalculateWrapTarget(Vector2 effectiveBallPos, Angle heading, float angleToTarget)
    {
        var headingDir = heading.ToUnitVec();
        var turnSign = angleToTarget > 0f ? 1f : -1f;
        var behind = -headingDir * (Context.RobotRadius + 20f);
        var sideOffset = (heading - Angle.FromDeg(turnSign * 90f)).ToUnitVec() * (Context.RobotRadius * 0.5f);
        return effectiveBallPos + behind + sideOffset;
    }

    public static OpenAngleSolution CalculateOpenAngleToGoal(Vector2 pos, Robot.Robot robot)
    {
        var midGoal = Context.Field.OppGoal();
        var topGoal = Context.Field.OppGoalPostTop();
        var bottomGoal = Context.Field.OppGoalPostBottom();

        topGoal += Vector2.Normalize(midGoal - topGoal) * 25f;
        bottomGoal += Vector2.Normalize(midGoal - bottomGoal) * 25f;

        var midGoalAngle = MathF.Atan2(midGoal.Y - pos.Y, midGoal.X - pos.X);
        var goalStart = MathF.Atan2(bottomGoal.Y - pos.Y, bottomGoal.X - pos.X);
        var goalEnd = MathF.Atan2(topGoal.Y - pos.Y, topGoal.X - pos.X);

        var wraps = MathF.Abs(goalStart - goalEnd) > MathF.PI;
        if (goalStart > goalEnd)
        {
            (goalStart, goalEnd) = (goalEnd, goalStart);
        }

        if (wraps)
        {
            (goalStart, goalEnd) = (goalEnd, goalStart);
        }

        Span<float> blockedStarts = stackalloc float[MaxObstacleArcs];
        Span<float> blockedEnds = stackalloc float[MaxObstacleArcs];
        var obstacleCount = 0;

        for (var i = 0; i < Context.OwnRobots.Count; i++)
        {
            var own = Context.OwnRobots[i];
            if (!own.Seen || own.Id == robot.Id)
            {
                continue;
            }

            AddObstacleArc(pos, own.Position, goalStart, goalEnd, wraps, blockedStarts, blockedEnds, ref obstacleCount);
        }

        for (var i = 0; i < Context.OppRobots.Count; i++)
        {
            AddObstacleArc(pos, Context.OppRobots[i].State.Position, goalStart, goalEnd, wraps, blockedStarts, blockedEnds, ref obstacleCount);
        }

        if (obstacleCount == 0)
        {
            return new OpenAngleSolution(Angle.FromRad(midGoalAngle), Angle.FromRad(MathF.Abs(goalEnd - goalStart)));
        }

        var step = (goalEnd - goalStart) / OpenAngleSamples;
        if (wraps)
        {
            step += 2f * MathF.PI / OpenAngleSamples;
        }

        Span<bool> blocked = stackalloc bool[OpenAngleSamples];
        for (var i = 0; i < OpenAngleSamples; i++)
        {
            var angle = goalStart + i * step;

            for (var j = 0; j < obstacleCount; j++)
            {
                var start = blockedStarts[j];
                var end = blockedEnds[j];
                var angleToCheck = angle;

                if (wraps)
                {
                    if (start < 0f) start += 2f * MathF.PI;
                    if (end < 0f) end += 2f * MathF.PI;
                    if (angleToCheck < 0f) angleToCheck += 2f * MathF.PI;
                }

                if (angleToCheck >= start && angleToCheck < end)
                {
                    blocked[i] = true;
                    break;
                }
            }
        }

        var maxFree = 0;
        var freeCount = 0;
        var bestSample = 0f;

        for (var i = 0; i < OpenAngleSamples; i++)
        {
            if (blocked[i])
            {
                if (freeCount > maxFree)
                {
                    maxFree = freeCount;
                    bestSample = i - maxFree * 0.5f;
                }

                freeCount = 0;
            }
            else
            {
                freeCount++;
            }
        }

        if (freeCount > maxFree)
        {
            maxFree = freeCount;
            bestSample = OpenAngleSamples - maxFree * 0.5f;
        }

        if (maxFree == 0)
        {
            return new OpenAngleSolution(Angle.FromRad(midGoalAngle), Angle.Zero);
        }

        var bestAngle = NormalizeAngleRad(goalStart + bestSample * step);
        return new OpenAngleSolution(Angle.FromRad(bestAngle), Angle.FromRad(maxFree * step));
    }

    private static void AddObstacleArc(
        Vector2 origin,
        Vector2 obstacle,
        float goalStart,
        float goalEnd,
        bool wraps,
        Span<float> blockedStarts,
        Span<float> blockedEnds,
        ref int obstacleCount)
    {
        if (obstacleCount >= MaxObstacleArcs)
        {
            return;
        }

        var distance = Vector2.Distance(origin, obstacle);
        if (Utils.ApproximatelyZero(distance))
        {
            return;
        }

        var start = MathF.Atan2(obstacle.Y - origin.Y, obstacle.X - origin.X);
        var delta = MathF.Abs(MathF.Atan(90f / distance));
        var end = start + delta;
        start -= delta;

        start = NormalizeAngleRad(start);
        end = NormalizeAngleRad(end);

        var startCompare = start;
        var endCompare = end;
        var goalStartCompare = goalStart;
        var goalEndCompare = goalEnd;

        if (wraps)
        {
            if (startCompare < 0f) startCompare += 2f * MathF.PI;
            if (endCompare < 0f) endCompare += 2f * MathF.PI;
            if (goalStartCompare < 0f) goalStartCompare += 2f * MathF.PI;
            if (goalEndCompare < 0f) goalEndCompare += 2f * MathF.PI;
        }

        if (endCompare > goalEndCompare && startCompare < goalStartCompare)
        {
            blockedStarts[0] = goalStart;
            blockedEnds[0] = goalEnd;
            obstacleCount = 1;
            return;
        }

        if (startCompare > goalEndCompare || endCompare < goalStartCompare)
        {
            return;
        }

        if (startCompare < goalStartCompare)
        {
            start = goalStart;
        }

        if (endCompare > goalEndCompare)
        {
            end = goalEnd;
        }

        blockedStarts[obstacleCount] = start;
        blockedEnds[obstacleCount] = end;
        obstacleCount++;
    }

    private static float NormalizeAngleRad(float angle)
    {
        while (angle > MathF.PI) angle -= 2f * MathF.PI;
        while (angle < -MathF.PI) angle += 2f * MathF.PI;
        return angle;
    }
}
