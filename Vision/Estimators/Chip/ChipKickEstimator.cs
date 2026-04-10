using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Extensions;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Estimators;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;
using CameraCalibration = Tyr.Common.Data.Ssl.Vision.Geometry.CameraCalibration;

namespace Tyr.Vision.Estimators.Chip;

[Configurable]
public partial class ChipKickEstimator : IKickEstimator
{
    private const float Gravity = 9810f;

    [ConfigEntry("Minimum number of records to start chip estimation")]
    private static int MinRecords { get; set; } = 8;

    [ConfigEntry("Max fitting failures until this chip estimator is dropped")]
    private static int MaxFailures { get; set; } = 12;

    [ConfigEntry("Max fitting error until this chip estimator is dropped [mm]")]
    private static double MaxFittingError { get; set; } = 100.0;

    [ConfigEntry("Max number of records to keep over all cameras")]
    private static int MaxNumberOfRecords { get; set; } = 50;

    [ConfigEntry("Estimate chip kick position if the ball is visible on two cameras")]
    private static bool UseKickPositionEstimator { get; set; } = false;

    private readonly IReadOnlyDictionary<uint, CameraCalibration> _cameraCalibrations;
    private readonly List<RawBall> _records = [];
    private readonly List<RawBall> _allRecords = [];
    private readonly Timestamp _kickEventTimestamp;
    private readonly ChipKickLinearTimeOffsetFitter _solverLin3;
    private readonly ChipKickLinearPositionTimeOffsetFitter _solverLin5;
    private readonly BallTrajectoryFactory _trajectoryFactory = new();

    private int _pruneIndex;
    private ChipKickNonlinearVelocityFitter? _solverNonlinear;
    private bool _doFirstHopFit = true;
    private IBallTrajectory? _currentTrajectory;
    private KickFitResult? _fitResult;
    private Timestamp _kickTimestamp;
    private double _avgDistanceLatest10;
    private int _failures;

    public ChipKickEstimator(
        IReadOnlyDictionary<uint, CameraCalibration> cameraCalibrations,
        DetectedKick kick)
    {
        _cameraCalibrations = cameraCalibrations;
        _kickEventTimestamp = kick.Timestamp;
        _kickTimestamp = kick.Timestamp;
        _solverLin3 = new ChipKickLinearTimeOffsetFitter(kick.KickPosition, kick.Timestamp, cameraCalibrations);
        _solverLin5 = new ChipKickLinearPositionTimeOffsetFitter(kick.KickPosition, kick.Timestamp, cameraCalibrations);

        var rawBalls = kick.BallStatesAfterKick
            .Select(ballState => ballState.LatestRawBall)
            .Where(rawBall => rawBall.HasValue)
            .Select(rawBall => rawBall!.Value)
            .OrderBy(ball => ball.CaptureTimestamp)
            .ThenBy(ball => ball.CameraId)
            .ThenBy(ball => ball.FrameNumber)
            .Where(ball => Vector2.Distance(ball.Detection.Position, kick.KickPosition) > 50f)
            .ToList();

        _records.AddRange(rawBalls);
        _allRecords.AddRange(rawBalls);
    }

    public KickEstimatorType Type => KickEstimatorType.Chip;

    public void AddCamBall(RawBall record)
    {
        _records.Add(record);
        _allRecords.Add(record);

        if (_records.Count < MinRecords)
        {
            return;
        }

        PruneRecords();

        var solvedKick = RunSolvers();
        if (solvedKick == null)
        {
            _failures++;
            _fitResult = null;
            return;
        }

        _kickTimestamp = solvedKick.Timestamp;

        if (_doFirstHopFit)
        {
            var flightTime = (2f * solvedKick.Velocity.Z) / Gravity;
            var lastRecord = _records[^1];

            if (((lastRecord.CaptureTimestamp - _kickTimestamp).Seconds > flightTime) && (_records.Count > 12))
            {
                _doFirstHopFit = false;
                _solverNonlinear = new ChipKickNonlinearVelocityFitter(
                    solvedKick.Position,
                    solvedKick.Timestamp,
                    _cameraCalibrations,
                    solvedKick.Velocity);
            }
        }

        _currentTrajectory = _trajectoryFactory.FromKickedBallWithoutSpin(solvedKick.Position, solvedKick.Velocity);
        ComputeFitResult(solvedKick.Name);
    }

    public KickFitResult? GetFitResult() => _fitResult;

    public bool IsDone(List<FilteredRobot> mergedRobots, Timestamp timestamp)
    {
        if (_records.Count == 0)
        {
            return false;
        }

        if (((_records[^1].CaptureTimestamp - _records[0].CaptureTimestamp).Seconds) < 0.1)
        {
            return false;
        }

        if (_failures > MaxFailures)
        {
            return true;
        }

        if (_fitResult == null)
        {
            return false;
        }

        if ((_fitResult.AvgDistance > MaxFittingError) || (_avgDistanceLatest10 > MaxFittingError))
        {
            return true;
        }

        return IsBallStateInvalid(mergedRobots, timestamp);
    }

    private bool IsBallStateInvalid(List<FilteredRobot> mergedRobots, Timestamp timestamp)
    {
        if (_fitResult == null)
        {
            return false;
        }

        var state = _fitResult.GetState(timestamp);
        var positionNow = state.Position;
        var minDistanceToRobot = mergedRobots
            .Select(robot => Vector2.Distance(robot.State.Position, positionNow))
            .DefaultIfEmpty(float.MaxValue)
            .Min();

        if ((minDistanceToRobot < Tyr.Vision.Vision.FieldSize.RobotRadius) && !state.IsChipped)
        {
            return true;
        }

        if ((state.Velocity.Length() < 1f) && (_records.Count > (MaxNumberOfRecords / 2)))
        {
            return true;
        }

        return !Tyr.Vision.Vision.FieldSize.Rectangle.Inside(positionNow, 100f);
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
            _pruneIndex = 0;
        }
    }

    private SolvedKick? RunSolvers()
    {
        SolvedKick? solvedKick;

        if (_doFirstHopFit)
        {
            solvedKick = _solverLin3.Solve(_records);
            SolvedKick? lin5SolvedKick = null;

            var groupedRecords = _records
                .GroupBy(record => record.CameraId)
                .ToList();

            if ((groupedRecords.Count > 1) && UseKickPositionEstimator)
            {
                lin5SolvedKick = _solverLin5.Solve(_records);
            }

            if ((solvedKick != null) && (lin5SolvedKick != null)
                && (_solverLin5.LastL1Error < _solverLin3.LastL1Error))
            {
                solvedKick = lin5SolvedKick;
            }
        }
        else
        {
            solvedKick = _solverNonlinear?.Solve(_records);
        }

        if (solvedKick == null)
        {
            return null;
        }

        var chipAngle = MathF.Atan2(solvedKick.Velocity.Z, solvedKick.Velocity.Xy().Length());
        if (chipAngle > (60f * MathF.PI / 180f))
        {
            return null;
        }

        return solvedKick;
    }

    private void ComputeFitResult(string solverName)
    {
        if (_currentTrajectory == null)
        {
            _fitResult = null;
            return;
        }

        var modelPoints = _records
            .Select(record =>
            {
                var modelPosition = _currentTrajectory.GetState(record.CaptureTimestamp - _kickTimestamp).Position3D;
                return BallProjection.ProjectToGround(
                    modelPosition,
                    BallProjection.GetCameraPosition(_cameraCalibrations, record.CameraId));
            })
            .ToList();

        var avgDistance = modelPoints
            .Zip(_records, (modelPoint, record) => Vector2.Distance(modelPoint, record.Detection.Position))
            .Average();

        if (_records.Count >= 10)
        {
            _avgDistanceLatest10 = modelPoints
                .Zip(_records, (modelPoint, record) => Vector2.Distance(modelPoint, record.Detection.Position))
                .Skip(_records.Count - 10)
                .Average();
        }

        _fitResult = new KickFitResult(modelPoints, avgDistance, _currentTrajectory, _kickTimestamp, solverName);
    }
}
