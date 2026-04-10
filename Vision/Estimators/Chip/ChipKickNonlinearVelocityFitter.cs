using System.Numerics;
using NLoptNet;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Chip;

public class ChipKickNonlinearVelocityFitter : IDisposable
{
    private const double FunctionTolerance = 1e-3;
    private const int MaxIterations = 20;
    private const double SimplexStep = 10.0;

    private readonly Vector2 _kickPosition;
    private readonly Timestamp _kickTimestamp;
    private readonly IReadOnlyDictionary<uint, CameraCalibration> _cameraCalibrations;
    private readonly double[] _kickVelocityGuess;
    private readonly NLoptSolver _optimizer;
    private ChipTrajectoryErrorModel? _currentModel;

    public ChipKickNonlinearVelocityFitter(
        Vector2 kickPosition,
        Timestamp kickTimestamp,
        IReadOnlyDictionary<uint, CameraCalibration> cameraCalibrations,
        Vector3 initialEstimate)
    {
        _kickPosition = kickPosition;
        _kickTimestamp = kickTimestamp;
        _cameraCalibrations = cameraCalibrations;
        _kickVelocityGuess = [initialEstimate.X, initialEstimate.Y, initialEstimate.Z];
        _optimizer = new NLoptSolver(NLoptAlgorithm.LN_SBPLX, 3, FunctionTolerance, MaxIterations);
        _optimizer.SetInitialStepSize1(SimplexStep);
        _optimizer.SetMinObjective(EvaluateObjective);
    }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0)
        {
            return null;
        }

        _currentModel = new ChipTrajectoryErrorModel(ballRecords, _kickPosition, _kickTimestamp, _cameraCalibrations);
        var startVelocityX = _kickVelocityGuess[0];
        var startVelocityY = _kickVelocityGuess[1];
        var startVelocityZ = _kickVelocityGuess[2];

        var resultX = startVelocityX;
        var resultY = startVelocityY;
        var resultZ = startVelocityZ;
        try
        {
            var result = _optimizer.Optimize(_kickVelocityGuess, out _);
            if (!IsUsableResult(result))
            {
                _kickVelocityGuess[0] = startVelocityX;
                _kickVelocityGuess[1] = startVelocityY;
                _kickVelocityGuess[2] = startVelocityZ;
            }

            resultX = _kickVelocityGuess[0];
            resultY = _kickVelocityGuess[1];
            resultZ = _kickVelocityGuess[2];
        }
        finally
        {
            _currentModel = null;
        }

        _kickVelocityGuess[0] = resultX;
        _kickVelocityGuess[1] = resultY;
        _kickVelocityGuess[2] = resultZ;

        return new SolvedKick(
            _kickPosition,
            new Vector3((float)resultX, (float)resultY, (float)resultZ),
            _kickTimestamp,
            Vector2.Zero,
            nameof(ChipKickNonlinearVelocityFitter));
    }

    public void Dispose() => _optimizer.Dispose();

    private static bool IsUsableResult(NloptResult result) =>
        ((int)result > 0) || (result == NloptResult.ROUNDOFF_LIMITED);

    private double EvaluateObjective(double[] point) => _currentModel!.Value(point[0], point[1], point[2]);

    private sealed class ChipTrajectoryErrorModel(
        List<RawBall> records,
        Vector2 kickPosition,
        Timestamp kickTimestamp,
        IReadOnlyDictionary<uint, CameraCalibration> cameraCalibrations)
    {
        public double Value(double velocityX, double velocityY, double velocityZ)
        {
            var kickVelocity = new Vector3((float)velocityX, (float)velocityY, (float)velocityZ);
            var error = 0.0;
            if (velocityZ > 0.0)
            {
                var trajectory = BallChip.FromKick(kickPosition, kickVelocity, Vector2.Zero);
                foreach (var ballRecord in records)
                {
                    var modelPosition = trajectory.GetState(ballRecord.CaptureTimestamp - kickTimestamp).Position3D;
                    var ground = BallProjection.ProjectToGround(
                        modelPosition,
                        BallProjection.GetCameraPosition(cameraCalibrations, ballRecord.CameraId));
                    error += Vector2.Distance(ground, ballRecord.Detection.Position);
                }
            }
            else
            {
                var trajectory = new BallFlat(new BallState
                {
                    Position3D = kickPosition.Xyz(),
                    Velocity = kickVelocity,
                    Acceleration = Vector3.Zero,
                    SpinRadians = Vector2.Zero
                });

                foreach (var ballRecord in records)
                {
                    var modelPosition = trajectory.GetState(ballRecord.CaptureTimestamp - kickTimestamp).Position3D;
                    var ground = BallProjection.ProjectToGround(
                        modelPosition,
                        BallProjection.GetCameraPosition(cameraCalibrations, ballRecord.CameraId));
                    error += Vector2.Distance(ground, ballRecord.Detection.Position);
                }
            }

            return error / records.Count;
        }
    }
}
