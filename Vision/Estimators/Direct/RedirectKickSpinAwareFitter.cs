using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using Tyr.Common.Math;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Direct;

public class RedirectKickSpinAwareFitter
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
    private readonly NelderMeadSimplex _optimizer = new(FunctionTolerance, MaxIterations);
    private readonly DenseVector _initialGuessVector = new(3);
    private readonly DenseVector _initialPerturbation = new(3);
    private readonly IObjectiveFunction _objectiveFunction;

    private Vector2? _fixedKickDirection;
    private bool _hasInitialGuess;
    private double _spinFactorState = BallParameters.RedirectSpinFactor;
    private RedirectBallModel? _currentModel;
    private double _bestKickX;
    private double _bestKickY;
    private double _bestKickVelocity;
    private double _bestError;

    public RedirectKickSpinAwareFitter(Angle kickingBotOrientation, FilteredBall ballStateAtKick)
    {
        _initialSpin = ballStateAtKick.State.SpinRadians;
        _kickingBotOrientation = kickingBotOrientation;
        _objectiveFunction = ObjectiveFunction.Value(EvaluateObjective);
    }

    public RedirectKickSpinAwareFitter(Angle kickingBotOrientation)
    {
        _initialSpin = Vector2.Zero;
        _kickingBotOrientation = kickingBotOrientation;
        _objectiveFunction = ObjectiveFunction.Value(EvaluateObjective);
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
        _bestKickX = _initialGuess[0];
        _bestKickY = _initialGuess[1];
        _bestKickVelocity = _initialGuess[2];
        _bestError = double.PositiveInfinity;

        _initialGuessVector[0] = _initialGuess[0];
        _initialGuessVector[1] = _initialGuess[1];
        _initialGuessVector[2] = _initialGuess[2];

        _initialPerturbation[0] = SimplexStep;
        _initialPerturbation[1] = SimplexStep;
        _initialPerturbation[2] = SimplexStep;

        double positionX;
        double positionY;
        double velocityMagnitude;
        double error;
        try
        {
            var result = _optimizer.FindMinimum(_objectiveFunction, _initialGuessVector, _initialPerturbation);
            var point = result.MinimizingPoint;
            positionX = point[0];
            positionY = point[1];
            velocityMagnitude = point[2];
            error = _currentModel.Value(positionX, positionY, velocityMagnitude);
        }
        catch (Exception) when (!double.IsPositiveInfinity(_bestError))
        {
            positionX = _bestKickX;
            positionY = _bestKickY;
            velocityMagnitude = _bestKickVelocity;
            error = _bestError;
        }
        finally
        {
            _currentModel = null;
        }

        _initialGuess[0] = positionX;
        _initialGuess[1] = positionY;
        _initialGuess[2] = velocityMagnitude;

        var kickPosition = new Vector2((float)positionX, (float)positionY);
        var kickVelocity = kickDirection * (float)velocityMagnitude;

        return new NonlinearSolveResult(kickPosition, kickVelocity, error);
    }

    private double EvaluateObjective(MathNet.Numerics.LinearAlgebra.Vector<double> point)
    {
        var error = _currentModel!.Value(point[0], point[1], point[2]);
        if (error < _bestError)
        {
            _bestError = error;
            _bestKickX = point[0];
            _bestKickY = point[1];
            _bestKickVelocity = point[2];
        }

        return error;
    }

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
