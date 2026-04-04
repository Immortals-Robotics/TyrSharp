using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Direct;

public class StraightKickFixedDirectionNonlinearFitter
{
    private const double FunctionTolerance = 1e-2;
    private const int MaxIterations = 2000;

    private readonly double[] _initialGuess = new double[3];

    public StraightKickFixedDirectionNonlinearFitter(Vector2 kickPosition, float kickVelocity)
    {
        _initialGuess[0] = kickPosition.X;
        _initialGuess[1] = kickPosition.Y;
        _initialGuess[2] = kickVelocity;
    }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0)
            return null;

        var tZero = ballRecords[0].CaptureTimestamp;
        var groundPos = ballRecords
            .Select(record => record.Detection.Position)
            .ToList();

        var estimated = BallHelpers.GetKickDirection(groundPos);
        if (estimated == null)
            return null;

        var (_, direction) = estimated.Value;
        var model = new StraightBallModel(ballRecords, direction, tZero);
        double[] result;
        try
        {
            result = Optimize(model.Value, _initialGuess);
        }
        catch (Exception)
        {
            return null;
        }

        Array.Copy(result, _initialGuess, _initialGuess.Length);

        var kickPosition = new Vector2((float)result[0], (float)result[1]);
        var kickVelocity = direction * (float)result[2];

        return new SolvedKick(
            kickPosition,
            new Vector3(kickVelocity.X, kickVelocity.Y, 0f),
            tZero,
            Vector2.Zero,
            nameof(StraightKickFixedDirectionNonlinearFitter));
    }

    private static double[] Optimize(Func<double[], double> objective, double[] initialGuess)
    {
        var optimizer = new NelderMeadSimplex(FunctionTolerance, MaxIterations);
        var objectiveFunction = ObjectiveFunction.Value(
            point => objective(point.ToArray()));
        var initialGuessVector = DenseVector.OfArray(initialGuess);
        var result = optimizer.FindMinimum(objectiveFunction, initialGuessVector);
        return result.MinimizingPoint.ToArray();
    }

    private sealed class StraightBallModel(List<RawBall> records, Vector2 kickDirection, Timestamp tZero)
    {
        public double Value(double[] point)
        {
            var kickPosition = new Vector2((float)point[0], (float)point[1]);
            var kickVelocity = kickDirection * (float)point[2];

            var trajectory = new BallFlat(new BallState
            {
                Position3D = new Vector3(kickPosition.X, kickPosition.Y, 0f),
                Velocity = new Vector3(kickVelocity.X, kickVelocity.Y, 0f),
                Acceleration = Vector3.Zero,
                SpinRadians = Vector2.Zero
            });

            double error = 0;
            foreach (var ball in records)
            {
                var modelPosition = trajectory.GetState(ball.CaptureTimestamp - tZero).Position;
                error += Vector2.Distance(modelPosition, ball.Detection.Position);
            }

            return error / records.Count;
        }
    }
}
