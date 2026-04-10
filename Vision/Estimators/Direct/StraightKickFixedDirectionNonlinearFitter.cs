using System.Numerics;
using NLoptNet;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Direct;

public class StraightKickFixedDirectionNonlinearFitter : IDisposable
{
    private const double FunctionTolerance = 1e-3;
    private const int MaxIterations = 200;
    private const double SimplexStep = 100.0;

    private readonly double[] _initialGuess = new double[3];
    private readonly NLoptSolver _optimizer;

    private StraightBallModel? _currentModel;

    public StraightKickFixedDirectionNonlinearFitter(Vector2 kickPosition, float kickVelocity)
    {
        _initialGuess[0] = kickPosition.X;
        _initialGuess[1] = kickPosition.Y;
        _initialGuess[2] = kickVelocity;
        _optimizer = new NLoptSolver(NLoptAlgorithm.LN_SBPLX, 3, FunctionTolerance, MaxIterations);
        _optimizer.SetInitialStepSize1(SimplexStep);
        _optimizer.SetMinObjective(EvaluateObjective);
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
        if (!TryOptimize(model, out var solvedX, out var solvedY, out var solvedVelocity))
            return null;

        _initialGuess[0] = solvedX;
        _initialGuess[1] = solvedY;
        _initialGuess[2] = solvedVelocity;

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
        var startX = _initialGuess[0];
        var startY = _initialGuess[1];
        var startVelocity = _initialGuess[2];

        x = startX;
        y = startY;
        velocity = startVelocity;

        try
        {
            var result = _optimizer.Optimize(_initialGuess, out _);
            if (!IsUsableResult(result))
            {
                _initialGuess[0] = startX;
                _initialGuess[1] = startY;
                _initialGuess[2] = startVelocity;
                return false;
            }

            x = _initialGuess[0];
            y = _initialGuess[1];
            velocity = _initialGuess[2];
            return true;
        }
        finally
        {
            _currentModel = null;
        }
    }

    public void Dispose() => _optimizer.Dispose();

    private static bool IsUsableResult(NloptResult result) =>
        ((int)result > 0) || (result == NloptResult.ROUNDOFF_LIMITED);

    private double EvaluateObjective(double[] point) => _currentModel!.Value(point[0], point[1], point[2]);

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
