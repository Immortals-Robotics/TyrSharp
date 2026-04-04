using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using MathNet.Numerics.LinearAlgebra;
using Vector = MathNet.Numerics.LinearAlgebra.Double.Vector;

namespace Tyr.Vision.Util;

[Configurable]
public partial class KickValidator
{
    // Distance

    [ConfigEntry("Minimum distance (in mm) that at least one ball sample must be from the robot to consider a kick")]
    private static double AtLeastOneBeyondDist { get; set; } = 160.0;

    [ConfigEntry(
        "Distance threshold (in mm) for kick validation: first sample must be closer, all others further away")]
    private static double ThresholdDist1 { get; set; } = 130.0;

    [ConfigEntry(
        "Alternative distance threshold (in mm) for kick validation: first sample must be closer, all others further away")]
    private static double ThresholdDist2 { get; set; } = 170.0;

    // Velocity

    [ConfigEntry("Minimum required ball velocity [mm/s]")]
    private static double MinVelocity { get; set; } = 600.0;

    // In Kicker
    [ConfigEntry("Minimum distance from bot to lead point on orientation line")]
    private static double MinDistanceInFront { get; set; } = 70.0;

    [ConfigEntry(
        "Minimum distance from orientation line to ball position (distance is increased with distance from bot)")]
    private static double MinDistanceOrthogonal { get; set; } = 40.0;

    [ConfigEntry("Maximum angle difference for a valid kick")]
    private static double MaxAngleDiff { get; set; } = 45.0;

    public bool Validate(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var validateDistance = DistanceValidator(balls, robots);
        var validateVelocity = VelocityValidator(balls);
        var validateGettingAway = GettingAwayValidator(balls, robots);
        var validateInKicker = InKickerValidator(balls, robots);

        Log.ZLogDebug(
            $"Kick validator: Distance: {validateDistance} Velocity: {validateVelocity} GettingAway: {validateGettingAway} InKicker: {validateInKicker}");
        return validateDistance & validateVelocity & validateGettingAway & validateInKicker;
    }

    public (Timestamp, Vector2)? DirectionBacktrack(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var robot = robots[0];

        var groupedBalls = balls
            .Where(b => b.LatestRawBall.HasValue)
            .GroupBy(b => b.LatestRawBall!.Value.CameraId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var smallestAngDiff = Math.PI;
        Vector2? kickPos = null;
        List<MergedBall>? bestGroup = null;

        foreach (var group in groupedBalls.Values)
        {
            if (group.Count < 3)
            {
                continue;
            }

            var ballPos = group
                .Where(b => b.LatestRawBall.HasValue)
                .Select(b => b.LatestRawBall!.Value.Detection.Position)
                .ToList();

            var kickDirection = BallHelpers.GetKickDirection(ballPos);

            if (kickDirection == null)
                return null;

            var (line, direction) = kickDirection.Value;

            var angDiff = Math.Abs(direction.AngleWith(robot.State.Angle.ToUnitVec()).DegNormalized);

            if (angDiff >= smallestAngDiff) continue;
            smallestAngDiff = angDiff;

            var kickerCenter = robot.State.Angle.ToUnitVec() * 105 + robot.State.Position;
            var front = Line.FromPointAndAngle(kickerCenter, robot.State.Angle + Angle.FromDeg(90));

            kickPos = Geometry.Intersection(line, front);
            if (kickPos == null) continue;
            kickPos = kickPos.Value;
            bestGroup = group;
        }

        if ((bestGroup == null) || !kickPos.HasValue) return null;

        var numPoints = bestGroup.Count;

        var matA = new DenseMatrix(numPoints, 2);
        var b = new DenseVector(numPoints);

        var firstBall = bestGroup[0].LatestRawBall!.Value;
        for (var i = 0; i < numPoints; i++)
        {
            var currentBall = bestGroup[i].LatestRawBall!.Value;

            matA[i, 0] = Vector2.Distance(currentBall.Detection.Position, kickPos.Value);
            matA[i, 1] = 1.0;
            b[i] = (currentBall.CaptureTimestamp - firstBall.CaptureTimestamp).Seconds;
        }

        try
        {
            var x = matA.QR().Solve(b);
            var kickTimestamp = Timestamp.FromNanoseconds(
                firstBall.CaptureTimestamp.Nanoseconds + (long)(x[1] * 1e9));
            return (kickTimestamp, kickPos.Value);
        }
        catch (ArgumentException)
        {
            return null;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private bool DistanceValidator(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var frameByCameraId =
            new Dictionary<uint, List<(MergedBall, FilteredRobot)>>();

        for (var i = 0; i < robots.Count; i++)
        {
            var cameraId = balls[i].LatestRawBall!.Value.CameraId;
            frameByCameraId.TryAdd(cameraId, []);
            frameByCameraId[cameraId].Add((balls[i], robots[i]));
        }

        foreach (var data in frameByCameraId.Values)
        {
            var distances = data
                .Select(d => Vector2.Distance(d.Item1.Position, d.Item2.State.Position))
                .ToList();

            var distantBall = distances.Any(d => d > AtLeastOneBeyondDist);

            if ((distances[0] < ThresholdDist1)
                && distances.Skip(1).All(d => d > ThresholdDist1)
                && distantBall)
            {
                return true;
            }

            if ((distances[0] < ThresholdDist2)
                && distances.Skip(1).All(d => d > ThresholdDist2)
                && distantBall)
            {
                return true;
            }
        }

        return false;
    }

    private bool VelocityValidator(List<MergedBall> balls)
    {
        var groupedBalls = balls
            .GroupBy(b => b.LatestRawBall!.Value.CameraId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var validSamples = 0;

        foreach (var group in groupedBalls.Values)
        {
            for (var i = 1; i < group.Count; i++)
            {
                var bPrev = group[i - 1].LatestRawBall!.Value;
                var bNow = group[i].LatestRawBall!.Value;
                var tPrev = bPrev.CaptureTimestamp;
                var tNow = bNow.CaptureTimestamp;
                var prev = bPrev.Detection.Position;
                var now = bNow.Detection.Position;

                if (tPrev == tNow)
                {
                    continue;
                }

                var vel = Vector2.Distance(prev, now) / ((tNow - tPrev).Seconds);

                if (vel > MinVelocity)
                {
                    validSamples++;
                }
            }

            if (validSamples >= 2)
            {
                return true;
            }
        }

        return false;
    }

    private bool InKickerValidator(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var bot = robots[0];

        var direction = new Vector2(MathF.Cos(bot.State.Angle.Rad), MathF.Sin(bot.State.Angle.Rad));
        Draw.DrawArrow(bot.State.Position, bot.State.Position + direction, Color.Blue, Options.Outline(15f));
        var botPos = bot.State.Position;

        foreach (var b in balls)
        {
            var ballPos = b.RawPosition;
            var toBall = ballPos - botPos;

            var projectionLength = Vector2.Dot(toBall, direction);
            var leadPoint = botPos + direction * projectionLength;

            if (projectionLength < 0)
            {
                return false;
            }

            var distBotToLeadPoint = Vector2.Distance(botPos, leadPoint);

            if (distBotToLeadPoint < MinDistanceInFront)
            {
                return false;
            }

            var distBallToLeadPoint = Vector2.Distance(ballPos, leadPoint);

            if (distBallToLeadPoint > (MinDistanceOrthogonal + (distBotToLeadPoint - MinDistanceInFront)))
            {
                return false;
            }
        }

        return true;
    }

    private bool GettingAwayValidator(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var bot = robots[0];

        var groupedBalls = balls
            .GroupBy(b => b.LatestRawBall!.Value.CameraId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var group in groupedBalls.Values)
        {
            var distances = group
                .Select(ball => Vector2.Distance(ball.LatestRawBall!.Value.Detection.Position, bot.State.Position))
                .ToList();

            if (distances.Count < 2)
            {
                continue;
            }

            var valid = Enumerable.Range(1, distances.Count - 1)
                .All(i => distances[i] > distances[i - 1]);

            if (valid)
            {
                return true;
            }
        }

        return false;
    }
}