using System.Numerics;
using NLoptNet;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Direct;

public class RedirectKickSpinAwareFitter : IDisposable
{
    private const int KickDirMaxRecordsPerCam = 5;
    private const double SimplexStep = 100.0;
    private const double FunctionTolerance = 1e-3;
    private const int MaxIterations = 50;
    private const double SpinFactorFilterAlpha = 0.95;
    private const double SpinFactorStep = 0.01;
    private const double SpinFactorSearchEpsilon = 0.001;

    private readonly Vector2 _initialSpin;
    private readonly Angle _kickingBotOrientation;
    private readonly double[] _initialGuess = new double[3];
    private readonly NLoptSolver _optimizer;

    private Vector2? _fixedKickDirection;
    private bool _hasInitialGuess;
    private double _spinFactorState = BallParameters.RedirectSpinFactor;
    private RedirectBallModel? _currentModel;

    public RedirectKickSpinAwareFitter(Angle kickingBotOrientation, FilteredBall ballStateAtKick)
    {
        _initialSpin = ballStateAtKick.State.SpinRadians;
        _kickingBotOrientation = kickingBotOrientation;
        _optimizer = CreateOptimizer();
    }

    public RedirectKickSpinAwareFitter(Angle kickingBotOrientation)
    {
        _initialSpin = Vector2.Zero;
        _kickingBotOrientation = kickingBotOrientation;
        _optimizer = CreateOptimizer();
    }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0 || IsNotRedirect())
            return null;

        var kickDirection = _fixedKickDirection ?? GetKickDirection(ballRecords);
        if (kickDirection == null)
            return null;

        ComputeFixedKickDirection(ballRecords, kickDirection.Value);
        EnsureInitialGuess(ballRecords, kickDirection.Value);

        var minFactor = 0d;
        var maxFactor = BallParameters.RedirectSpinFactor
                        + ((1.0 - BallParameters.RedirectSpinFactor) * 0.3);

        var spinFactor = (minFactor + maxFactor) / 2.0;
        var increment = spinFactor / 2.0;

        while (increment > (2.0 * SpinFactorSearchEpsilon))
        {
            var resultLow = SolveNonlinear(
                ballRecords,
                kickDirection.Value,
                _initialSpin * (float)(spinFactor - SpinFactorSearchEpsilon));
            var resultHigh = SolveNonlinear(
                ballRecords,
                kickDirection.Value,
                _initialSpin * (float)(spinFactor + SpinFactorSearchEpsilon));

            if (resultLow.Error < resultHigh.Error)
            {
                spinFactor -= increment;
            }
            else
            {
                spinFactor += increment;
            }

            increment /= 2.0;
        }

        _spinFactorState = UpdateSpinFactorState(spinFactor);

        var solvedSpin = _initialSpin * (float)_spinFactorState;
        var solved = SolveNonlinear(ballRecords, kickDirection.Value, solvedSpin);

        return new SolvedKick(
            solved.KickPosition,
            new Vector3(solved.KickVelocity.X, solved.KickVelocity.Y, 0f),
            ballRecords[0].CaptureTimestamp,
            solvedSpin,
            nameof(RedirectKickSpinAwareFitter));
    }

    private bool IsNotRedirect()
    {
        // If the inbound spin corresponds to less than 1m/s surface speed, treat it as a normal straight kick.
        return (_initialSpin * BallParameters.Radius).Length() <= 1000f;
    }

    private static Vector2? GetKickDirection(List<RawBall> ballRecords)
    {
        var estimated = BallHelpers.GetKickDirectionByRegressionLine(ballRecords
            .Select(record => record.Detection.Position)
            .ToList());
        return estimated?.Item2;
    }

    private void ComputeFixedKickDirection(List<RawBall> ballRecords, Vector2 kickDirection)
    {
        if (_fixedKickDirection != null)
            return;

        var atLeastOneGroupAtMaxRecords = ballRecords
            .GroupBy(record => record.CameraId)
            .Any(group => group.Count() >= KickDirMaxRecordsPerCam);

        if (atLeastOneGroupAtMaxRecords)
        {
            // Once we have enough records on one camera, keep the already estimated direction
            // so later pruning does not make the solver chase a moving target.
            _fixedKickDirection = kickDirection;
        }
    }

    private double UpdateSpinFactorState(double measurement)
    {
        _spinFactorState = (SpinFactorFilterAlpha * _spinFactorState)
                           + ((1.0 - SpinFactorFilterAlpha) * measurement);
        return _spinFactorState;
    }

    private void EnsureInitialGuess(List<RawBall> ballRecords, Vector2 kickDirection)
    {
        if (_hasInitialGuess)
            return;

        var firstPosition = ballRecords[0].Detection.Position;
        _initialGuess[0] = firstPosition.X;
        _initialGuess[1] = firstPosition.Y;

        if (ballRecords.Count >= 2)
        {
            var dt = (ballRecords[^1].CaptureTimestamp - ballRecords[0].CaptureTimestamp).Seconds;
            if (dt > 0)
            {
                var travel = ballRecords[^1].Detection.Position - firstPosition;
                _initialGuess[2] = Math.Max(0.0, Vector2.Dot(travel, kickDirection) / dt);
            }
        }

        _hasInitialGuess = true;
    }

    private NonlinearSolveResult SolveNonlinear(List<RawBall> ballRecords, Vector2 kickDirection, Vector2 kickSpin)
    {
        _currentModel = new RedirectBallModel(ballRecords, kickSpin, kickDirection, ballRecords[0].CaptureTimestamp);
        var startPositionX = _initialGuess[0];
        var startPositionY = _initialGuess[1];
        var startVelocity = _initialGuess[2];

        var positionX = startPositionX;
        var positionY = startPositionY;
        var velocityMagnitude = startVelocity;
        double solvedError;
        try
        {
            var result = _optimizer.Optimize(_initialGuess, out var error);
            if (!IsUsableResult(result))
            {
                _initialGuess[0] = startPositionX;
                _initialGuess[1] = startPositionY;
                _initialGuess[2] = startVelocity;
            }

            positionX = _initialGuess[0];
            positionY = _initialGuess[1];
            velocityMagnitude = _initialGuess[2];
            solvedError = error ?? _currentModel!.Value(positionX, positionY, velocityMagnitude);
        }
        finally
        {
            _currentModel = null;
        }

        var kickPosition = new Vector2((float)positionX, (float)positionY);
        var kickVelocity = kickDirection * (float)velocityMagnitude;

        return new NonlinearSolveResult(
            kickPosition,
            kickVelocity,
            solvedError);
    }

    public void Dispose() => _optimizer.Dispose();

    private NLoptSolver CreateOptimizer()
    {
        var optimizer = new NLoptSolver(NLoptAlgorithm.LN_SBPLX, 3, FunctionTolerance, MaxIterations);
        optimizer.SetInitialStepSize1(SimplexStep);
        optimizer.SetMinObjective(EvaluateObjective);
        return optimizer;
    }

    private static bool IsUsableResult(NloptResult result) =>
        ((int)result > 0) || (result == NloptResult.ROUNDOFF_LIMITED);

    private double EvaluateObjective(double[] point) => _currentModel!.Value(point[0], point[1], point[2]);

    private sealed record NonlinearSolveResult(Vector2 KickPosition, Vector2 KickVelocity, double Error);

    private sealed class RedirectBallModel(
        List<RawBall> records,
        Vector2 kickSpin,
        Vector2 kickDirection,
        Timestamp tZero)
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
                SpinRadians = kickSpin
            });

            double error = 0;
            foreach (var ball in records)
            {
                var modelPosition = trajectory.GetState(ball.CaptureTimestamp - tZero).Position;
                error += Vector2.DistanceSquared(modelPosition, ball.Detection.Position);
            }

            return error / records.Count;
        }
    }
}
