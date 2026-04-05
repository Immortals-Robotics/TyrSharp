using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Time;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Chip;

public class ChipKickNonlinearVelocityFitter
{
    private const double FunctionTolerance = 1e-3;
    private const int MaxIterations = 200;
    private const double SimplexStep = 10.0;

    private readonly Vector2 _kickPosition;
    private readonly Timestamp _kickTimestamp;
    private readonly IReadOnlyDictionary<uint, CameraCalibration> _cameraCalibrations;
    private readonly double[] _kickVelocityGuess;
    private readonly BallTrajectoryFactory _trajectoryFactory = new();

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
    }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0)
        {
            return null;
        }

        var optimizer = new NelderMeadSimplex(FunctionTolerance, MaxIterations);
        var objective = new ChipTrajectoryErrorModel(ballRecords, _kickPosition, _kickTimestamp, _cameraCalibrations, _trajectoryFactory);

        double[] result;
        try
        {
            result = optimizer.FindMinimum(
                    ObjectiveFunction.Value(point => objective.Value(point.ToArray())),
                    DenseVector.OfArray(_kickVelocityGuess),
                    DenseVector.OfArray([SimplexStep, SimplexStep, SimplexStep]))
                .MinimizingPoint
                .ToArray();
        }
        catch (Exception)
        {
            result = (double[])_kickVelocityGuess.Clone();
        }

        Array.Copy(result, _kickVelocityGuess, _kickVelocityGuess.Length);

        return new SolvedKick(
            _kickPosition,
            new Vector3((float)result[0], (float)result[1], (float)result[2]),
            _kickTimestamp,
            Vector2.Zero,
            nameof(ChipKickNonlinearVelocityFitter));
    }

    private sealed class ChipTrajectoryErrorModel(
        List<RawBall> records,
        Vector2 kickPosition,
        Timestamp kickTimestamp,
        IReadOnlyDictionary<uint, CameraCalibration> cameraCalibrations,
        BallTrajectoryFactory trajectoryFactory)
    {
        public double Value(double[] point)
        {
            var kickVelocity = new Vector3((float)point[0], (float)point[1], (float)point[2]);
            var trajectory = trajectoryFactory.FromKickedBallWithoutSpin(kickPosition, kickVelocity);

            var error = 0.0;
            foreach (var ballRecord in records)
            {
                var modelPosition = trajectory.GetState(ballRecord.CaptureTimestamp - kickTimestamp).Position3D;
                var ground = BallProjection.ProjectToGround(
                    modelPosition,
                    BallProjection.GetCameraPosition(cameraCalibrations, ballRecord.CameraId));
                error += Vector2.Distance(ground, ballRecord.Detection.Position);
            }

            return error / records.Count;
        }
    }
}
