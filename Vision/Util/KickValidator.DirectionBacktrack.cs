using System.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Common.Math;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Vector = MathNet.Numerics.LinearAlgebra.Double.Vector;

namespace Tyr.Vision.Util;

public partial class KickValidator
{
    private readonly List<Vector2> _directionBallPositions = [];

    public (Timestamp, Vector2)? DirectionBacktrack(List<MergedBall> balls, List<FilteredRobot> robots)
    {
        var robot = robots[0];

        try
        {
            PrepareBallsByCamera(balls);

            var smallestAngDiff = Math.PI;
            Vector2? kickPos = null;
            List<MergedBall>? bestGroup = null;

            foreach (var group in _ballsByCamera.Values)
            {
                if (group.Count < 3)
                {
                    continue;
                }

                _directionBallPositions.Clear();
                for (var i = 0; i < group.Count; i++)
                {
                    if (!group[i].LatestRawBall.HasValue)
                    {
                        continue;
                    }

                    _directionBallPositions.Add(group[i].LatestRawBall!.Value.Detection.Position);
                }

                var kickDirection = BallHelpers.GetKickDirection(_directionBallPositions);

                if (kickDirection == null)
                {
                    return null;
                }

                var (line, direction) = kickDirection.Value;

                var angDiff = Math.Abs(direction.AngleWith(robot.State.Angle.ToUnitVec()).DegNormalized);
                if (angDiff >= smallestAngDiff)
                {
                    continue;
                }

                var kickerCenter = robot.State.Angle.ToUnitVec() * 105 + robot.State.Position;
                var front = Line.FromPointAndAngle(kickerCenter, robot.State.Angle + Angle.FromDeg(90));

                var intersection = Geometry.Intersection(line, front);
                if (intersection == null)
                {
                    continue;
                }

                smallestAngDiff = angDiff;
                kickPos = intersection.Value;
                bestGroup = group;
            }

            if ((bestGroup == null) || !kickPos.HasValue)
            {
                return null;
            }

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
        finally
        {
            _directionBallPositions.Clear();
            ClearBallsByCamera();
        }
    }
}
