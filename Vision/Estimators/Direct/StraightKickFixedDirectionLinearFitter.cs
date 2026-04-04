using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Vision.Data;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Direct;

public class StraightKickFixedDirectionLinearFitter
{
    public static SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0)
            return null;

        var numRecords = ballRecords.Count;
        var tZero = ballRecords[0].CaptureTimestamp;
        double acc = BallParameters.EffectiveAccelerationSlide;

        List<Vector2> groundPos = ballRecords
            .Select(record => record.Detection.Position)
            .ToList();

        var estimated = BallHelpers.GetKickDirectionByRegressionLine(groundPos);
        if (estimated == null)
            return null;
        var (_, dir) = estimated.Value;

        var matA = new DenseMatrix(numRecords * 2, 3);
        var b = new DenseVector(numRecords * 2);

        for (var i = 0; i < numRecords; i++)
        {
            var ballRecord = ballRecords[i];
            var position = ballRecord.Detection.Position;
            var t = (ballRecord.CaptureTimestamp - tZero).Seconds;

            matA.SetRow(i * 2, [1.0, 0.0, dir.X * t]);
            matA.SetRow((i * 2) + 1, [0.0, 1.0, dir.Y * t]);

            b[i * 2] = position.X - (0.5 * dir.X * t * t * acc);
            b[(i * 2) + 1] = position.Y - (0.5 * dir.Y * t * t * acc);
        }

        try
        {
            var x = matA.QR().Solve(b);
            var kickPosition = new Vector2((float)x[0], (float)x[1]);
            var kickVelocity = new Vector3(
                (float)(dir.X * x[2]),
                (float)(dir.Y * x[2]),
                0f);

            return new SolvedKick(
                kickPosition,
                kickVelocity,
                ballRecords[0].CaptureTimestamp,
                Vector2.Zero,
                nameof(StraightKickFixedDirectionLinearFitter));
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
}
