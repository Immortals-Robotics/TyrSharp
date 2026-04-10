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
    private const double FunctionTolerance = 1e-3;
    private const int MaxIterations = 100;
    private const double SimplexStep = 10.0;

    private readonly double[] _initialGuess = new double[3];
    private readonly NelderMeadSimplex _optimizer = new(FunctionTolerance, MaxIterations);
    private readonly DenseVector _initialGuessVector = new(3);
    private readonly DenseVector _initialPerturbation = new(3);
    private readonly IObjectiveFunction _objectiveFunction;

    private StraightBallModel? _currentModel;
    private double _bestX;
    private double _bestY;
    private double _bestVelocity;
    private double _bestError;

    public StraightKickFixedDirectionNonlinearFitter(Vector2 kickPosition, float kickVelocity)
    {
        _initialGuess[0] = kickPosition.X;
        _initialGuess[1] = kickPosition.Y;
        _initialGuess[2] = kickVelocity;
        _objectiveFunction = ObjectiveFunction.Value(EvaluateObjective);
    }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0)
            return null;

        var tZero = ballRecords[0].CaptureTimestamp;
        var groundPos = ballRecords
            .Select(record => record.Detection.Position)
            .ToList();

        var estimated = BallHelpers.GetKickDirectionByRegressionLine(groundPos);
        if (estimated == null)
            return null;

        var (_, direction) = estimated.Value;
        var model = new StraightBallModel(ballRecords, direction, tZero);
        try
        {
            if (!TryOptimize(model, out var solvedX, out var solvedY, out var solvedVelocity))
                return null;

            _initialGuess[0] = solvedX;
            _initialGuess[1] = solvedY;
            _initialGuess[2] = solvedVelocity;
        }
        catch (Exception)
        {
            return null;
        }

        var kickPosition = new Vector2((float)_initialGuess[0], (float)_initialGuess[1]);
        var kickVelocity = direction * (float)_initialGuess[2];

        return new SolvedKick(
            kickPosition,
            new Vector3(kickVelocity.X, kickVelocity.Y, 0f),
            tZero,
            Vector2.Zero,
            nameof(StraightKickFixedDirectionNonlinearFitter));
    }

    private bool TryOptimize(StraightBallModel model, out double x, out double y, out double velocity)
    {
        _currentModel = model;
        _bestX = _initialGuess[0];
        _bestY = _initialGuess[1];
        _bestVelocity = _initialGuess[2];
        _bestError = double.PositiveInfinity;

        _initialGuessVector[0] = _initialGuess[0];
        _initialGuessVector[1] = _initialGuess[1];
        _initialGuessVector[2] = _initialGuess[2];

        _initialPerturbation[0] = SimplexStep;
        _initialPerturbation[1] = SimplexStep;
        _initialPerturbation[2] = SimplexStep;

        try
        {
            var result = _optimizer.FindMinimum(_objectiveFunction, _initialGuessVector, _initialPerturbation);
            var point = result.MinimizingPoint;
            x = point[0];
            y = point[1];
            velocity = point[2];
            return true;
        }
        catch (Exception) when (!double.IsPositiveInfinity(_bestError))
        {
            x = _bestX;
            y = _bestY;
            velocity = _bestVelocity;
            return true;
        }
        finally
        {
            _currentModel = null;
        }
    }

    private double EvaluateObjective(MathNet.Numerics.LinearAlgebra.Vector<double> point)
    {
        var error = _currentModel!.Value(point[0], point[1], point[2]);
        if (error < _bestError)
        {
            _bestError = error;
            _bestX = point[0];
            _bestY = point[1];
            _bestVelocity = point[2];
        }

        return error;
    }

    private sealed class StraightBallModel(List<RawBall> records, Vector2 kickDirection, Timestamp tZero)
    {
        public double Value(double positionX, double positionY, double velocityMagnitude)
        {
            var kickPosition = new Vector2((float)positionX, (float)positionY);
            var kickVelocity = kickDirection * (float)velocityMagnitude;

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
