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
public partial class DirectKickEstimator
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

    private int _pruneIndex = 1;
    private Line? _fitLastLine;
    private DirectKickFitResult? _fitResult;
    private List<DirectKickFitResult> _activeSolvers = [];

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

        Log.ZLogDebug(
            $"DirectKickEstimator[{GetHashCode():X8}] created: rawBalls={rawBalls.Count}, filteredHistory={(filteredBalls?.Count ?? 0)}, avgKickSpeed={avgKickSpeed:F2}, hasBallStateAtKick={ballStateAtKick.HasValue}");

        RunSolvers();
    }

    public DirectKickFitResult? FitResult => _fitResult;
    public IReadOnlyList<DirectKickFitResult> ActiveSolvers => _activeSolvers;
    public Line? FitLastLine => _fitLastLine;

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
        Log.ZLogDebug(
            $"DirectKickEstimator[{GetHashCode():X8}].AddCamBall: cam={newRecord.CameraId}, frame={newRecord.FrameNumber}, ts={newRecord.CaptureTimestamp.Seconds:F3}, recordsBefore={_records.Count}, allRecordsBefore={_allRecords.Count}");
        _records.Add(newRecord);
        _allRecords.Add(newRecord);

        PruneRecords();
        RunSolvers();
    }

    public DirectKickFitResult? GetFitResult() => _fitResult;

    public bool IsDone(List<FilteredRobot> mergedRobots, Timestamp timestamp)
    {
        if (_allRecords.Count == 0)
        {
            Log.ZLogDebug($"DirectKickEstimator[{GetHashCode():X8}].IsDone=false: no records");
            return false;
        }

        if (((_allRecords[^1].CaptureTimestamp - _allRecords[0].CaptureTimestamp).Seconds) < 0.1)
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].IsDone=false: record span={(_allRecords[^1].CaptureTimestamp - _allRecords[0].CaptureTimestamp).Seconds:F3} < 0.1");
            return false;
        }

        if ((_allRecords.Count > 20) && IsMaxDirectionErrorExceeded(_allRecords))
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].IsDone=true: max direction error exceeded");
            return true;
        }

        if (_fitResult == null)
        {
            Log.ZLogDebug($"DirectKickEstimator[{GetHashCode():X8}].IsDone=false: no fit result");
            return false;
        }

        if (_fitResult.AvgDistance > MaxFittingError)
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].IsDone=true: avgDistance={_fitResult.AvgDistance:F3} > max={MaxFittingError:F3}");
            return true;
        }

        var posNow = _fitResult.GetState(timestamp).Position;
        var minDistToRobot = mergedRobots
            .Select(robot => Vector2.Distance(robot.State.Position, posNow))
            .DefaultIfEmpty(float.MaxValue)
            .Min();

        if (minDistToRobot < Tyr.Vision.Vision.FieldSize.RobotRadius.GetValueOrDefault(90f))
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].IsDone=true: ball reached robot, minDist={minDistToRobot:F3}");
            return true;
        }

        var insideField = Tyr.Vision.Vision.FieldSize.Rectangle.Inside(posNow, 100f);
        if (!insideField)
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].IsDone=true: ball left field at {posNow}");
        }
        else
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].IsDone=false: fit ok, inside field, minDistToRobot={minDistToRobot:F3}, avgDistance={_fitResult.AvgDistance:F3}");
        }

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

        Log.ZLogDebug(
            $"DirectKickEstimator[{GetHashCode():X8}].PruneRecords removing index {_pruneIndex} from {_records.Count} records");
        _records.RemoveAt(_pruneIndex);
        _pruneIndex++;

        if (_pruneIndex > (_records.Count - (MaxNumberOfRecords / 5)))
        {
            _pruneIndex = 1;
        }
    }

    private void RunSolvers()
    {
        Log.ZLogDebug(
            $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers start: records={_records.Count}, allRecords={_allRecords.Count}");
        var results = new List<DirectKickFitResult>();

        var sliding = StraightKickFixedDirectionLinearFitter.Solve(_records);
        if (sliding != null)
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers linear solver succeeded");
            results.Add(GenerateFitResult(sliding));
        }
        else
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers linear solver returned null");
        }

        var full = _solverFull.Solve(_records);
        if (full != null)
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers nonlinear solver succeeded");
            results.Add(GenerateFitResult(full));
        }
        else
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers nonlinear solver returned null");
        }

        var redirect = _solverRedirect.Solve(_records);
        if (redirect != null)
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers redirect solver succeeded");
            var redirectFit = GenerateFitResult(redirect);
            results.Add(new DirectKickFitResult(
                redirectFit.GroundProjection,
                redirectFit.AvgDistance * 0.5,
                redirectFit.Trajectory,
                redirectFit.KickTimestamp,
                redirectFit.SolverName));
        }
        else
        {
            Log.ZLogDebug(
                $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers redirect solver returned null");
        }

        _activeSolvers = results;
        _fitResult = results.MinBy(result => result.AvgDistance);

        Log.ZLogDebug(
            $"DirectKickEstimator[{GetHashCode():X8}].RunSolvers end: activeSolvers={_activeSolvers.Count}, hasBestFit={_fitResult != null}, bestAvgDistance={_fitResult?.AvgDistance}");
    }

    private DirectKickFitResult GenerateFitResult(SolvedKick result)
    {
        var ground = new List<Vector2>(_records.Count);
        var trajectory = new BallFlat(new BallState
        {
            Position3D = result.Position.Xyz(),
            Velocity = result.Velocity,
            Acceleration = Vector3.Zero,
            SpinRadians = result.Spin
        });

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

        return new DirectKickFitResult(ground, error, trajectory, result.Timestamp, result.Name);
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
