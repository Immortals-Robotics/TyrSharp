using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Direct;

[Configurable]
public partial class DirectKickEstimator : IKickEstimator
{
    [ConfigEntry("Max fitting error until this estimator is dropped [mm]")]
    private static double MaxFittingError { get; set; } = 100.0;

    [ConfigEntry("Max direction deviation until this estimator is dropped [deg]")]
    private static double MaxDirectionErrorDegrees { get; set; } = 20.0;

    [ConfigEntry("Max number of records to keep over all cameras")]
    private static int MaxNumberOfRecords { get; set; } = 50;

    private readonly List<RawBall> _records = [];
    private readonly List<RawBall> _allRecords = [];
    private readonly StraightKickFixedDirectionNonlinearFitter _solverFull;
    private readonly RedirectKickSpinAwareFitter _solverRedirect;
    private readonly BallTrajectoryFactory _trajectoryFactory = new();

    private int _pruneIndex = 1;
    private Line? _fitLastLine;
    private KickFitResult? _fitResult;
    private List<KickFitResult> _activeSolvers = [];

    public DirectKickEstimator(DetectedKick kick, IReadOnlyList<FilteredBall>? filteredBalls = null)
    {
        var rawBalls = kick.BallStatesAfterKick
            .Select(ballState => ballState.LatestRawBall)
            .Where(rawBall => rawBall.HasValue)
            .Select(rawBall => rawBall!.Value)
            .OrderBy(ball => ball.CaptureTimestamp)
            .ThenBy(ball => ball.CameraId)
            .ThenBy(ball => ball.FrameNumber)
            .ToList();

        if (rawBalls.Count > 2 && ((rawBalls[1].CaptureTimestamp - rawBalls[0].CaptureTimestamp).Seconds > 0.1))
        {
            // Ignore a stale pre-kick sample that can survive detector handoff.
            rawBalls.RemoveAt(0);
        }

        var ballStateAtKick = TryGetBallStateAtKick(kick, filteredBalls ?? Array.Empty<FilteredBall>());

        _records.AddRange(rawBalls);
        _allRecords.AddRange(rawBalls);

        var avgKickSpeed = GetKickSpeed(rawBalls, kick.KickPosition);
        _solverFull = new StraightKickFixedDirectionNonlinearFitter(kick.KickPosition, (float)avgKickSpeed);
        _solverRedirect = ballStateAtKick.HasValue
            ? new RedirectKickSpinAwareFitter(kick.RobotState.Angle, ballStateAtKick.Value)
            : new RedirectKickSpinAwareFitter(kick.RobotState.Angle);

        RunSolvers();
    }

    public KickFitResult? FitResult => _fitResult;
    public IReadOnlyList<KickFitResult> ActiveSolvers => _activeSolvers;
    public Line? FitLastLine => _fitLastLine;
    public KickEstimatorType Type => KickEstimatorType.Flat;

    public static double GetKickSpeed(IReadOnlyList<RawBall> balls, Vector2 kickPos)
    {
        if (balls.Count == 0)
            return 0;

        var numPoints = balls.Count;
        var matA = new DenseMatrix(numPoints, 2);
        var b = new DenseVector(numPoints);
        var tZero = balls[0].CaptureTimestamp;

        for (var i = 0; i < numPoints; i++)
        {
            var time = (balls[i].CaptureTimestamp - tZero).Seconds;
            matA[i, 0] = time;
            matA[i, 1] = 1.0;
            b[i] = Vector2.Distance(balls[i].Detection.Position, kickPos);
        }

        try
        {
            var x = matA.QR().Solve(b);
            return x[0] < 0 ? 0 : x[0];
        }
        catch (ArgumentException)
        {
            return 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
    }

    public void AddCamBall(RawBall newRecord)
    {
        _records.Add(newRecord);
        _allRecords.Add(newRecord);

        PruneRecords();
        RunSolvers();
    }

    public KickFitResult? GetFitResult() => _fitResult;

    public bool IsDone(List<FilteredRobot> mergedRobots, Timestamp timestamp)
    {
        if (_allRecords.Count == 0)
        {
            return false;
        }

        if (((_allRecords[^1].CaptureTimestamp - _allRecords[0].CaptureTimestamp).Seconds) < 0.1)
        {
            return false;
        }

        if ((_allRecords.Count > 20) && IsMaxDirectionErrorExceeded(_allRecords))
        {
            return true;
        }

        if (_fitResult == null)
        {
            return false;
        }

        if (_fitResult.AvgDistance > MaxFittingError)
        {
            return true;
        }

        var posNow = _fitResult.GetState(timestamp).Position;
        var minDistToRobot = mergedRobots
            .Select(robot => Vector2.Distance(robot.State.Position, posNow))
            .DefaultIfEmpty(float.MaxValue)
            .Min();

        if (minDistToRobot < Tyr.Vision.Vision.FieldSize.RobotRadius.GetValueOrDefault(90f))
        {
            return true;
        }

        var insideField = Tyr.Vision.Vision.FieldSize.Rectangle.Inside(posNow, 100f);
        return !insideField;
    }

    private static FilteredBall? TryGetBallStateAtKick(DetectedKick kick, IReadOnlyList<FilteredBall> filteredBalls)
    {
        var kickDirection = kick.RobotState.Angle.ToUnitVec();

        for (var i = filteredBalls.Count - 1; i > 1; i--)
        {
            var last = filteredBalls[i];
            var prev = filteredBalls[i - 1];

            var lastVelocity = last.State.Velocity.Xy();
            var prevVelocity = prev.State.Velocity.Xy();

            var travelDirectionAwayFromKicker = Vector2.Dot(lastVelocity, kickDirection) > 0f;
            var rapidlyDeceleratingBall = prevVelocity.LengthSquared() > (lastVelocity.LengthSquared() * 1.25f);
            var increasingBallSpeed = prevVelocity.LengthSquared() < lastVelocity.LengthSquared();

            if (travelDirectionAwayFromKicker || rapidlyDeceleratingBall || increasingBallSpeed)
            {
                continue;
            }

            return last;
        }

        return null;
    }

    private void PruneRecords()
    {
        if (_records.Count < MaxNumberOfRecords)
        {
            return;
        }
        _records.RemoveAt(_pruneIndex);
        _pruneIndex++;

        if (_pruneIndex > (_records.Count - (MaxNumberOfRecords / 5)))
        {
            _pruneIndex = 1;
        }
    }

    private void RunSolvers()
    {
        var results = new List<KickFitResult>();

        var sliding = StraightKickFixedDirectionLinearFitter.Solve(_records);
        if (sliding != null)
        {
            results.Add(GenerateFitResult(sliding));
        }

        var full = _solverFull.Solve(_records);
        if (full != null)
        {
            results.Add(GenerateFitResult(full));
        }

        var redirect = _solverRedirect.Solve(_records);
        if (redirect != null)
        {
            var redirectFit = GenerateFitResult(redirect);
            results.Add(new KickFitResult(
                redirectFit.GroundProjection,
                redirectFit.AvgDistance * 0.5,
                redirectFit.Trajectory,
                redirectFit.KickTimestamp,
                redirectFit.SolverName));
        }

        _activeSolvers = results;
        _fitResult = results.MinBy(result => result.AvgDistance);
    }

    private KickFitResult GenerateFitResult(SolvedKick result)
    {
        var ground = new List<Vector2>(_records.Count);
        var trajectory = _trajectoryFactory.FromKickedBall(
            result.Position,
            result.Velocity,
            result.Spin);

        double error = 0;
        foreach (var ball in _records)
        {
            var modelPos = trajectory.GetState(ball.CaptureTimestamp - result.Timestamp).Position;
            ground.Add(modelPos);
            error += Vector2.Distance(modelPos, ball.Detection.Position);
        }

        if (_records.Count > 0)
        {
            error /= _records.Count;
        }

        return new KickFitResult(ground, error, trajectory, result.Timestamp, result.Name);
    }

    private bool IsMaxDirectionErrorExceeded(IReadOnlyList<RawBall> records)
    {
        var lastRecords = records
            .TakeLast(Math.Min(10, records.Count))
            .Select(ball => ball.Detection.Position)
            .ToList();

        var lastLine = BallHelpers.GetKickDirectionByRegressionLine(lastRecords);
        _fitLastLine = lastLine?.Item1;

        if ((lastLine == null) || (_fitResult == null))
        {
            return false;
        }

        var firstDir = _fitResult.KickVelocity.Xy();
        var lastDir = lastLine.Value.Item2;

        if ((firstDir == Vector2.Zero) || (lastDir == Vector2.Zero))
        {
            return false;
        }

        var dot = Math.Abs(Vector2.Dot(Vector2.Normalize(firstDir), Vector2.Normalize(lastDir)));
        dot = Math.Clamp(dot, 0f, 1f);
        var angleDeg = MathF.Acos(dot) * (180f / MathF.PI);
        return angleDeg > MaxDirectionErrorDegrees;
    }

}
