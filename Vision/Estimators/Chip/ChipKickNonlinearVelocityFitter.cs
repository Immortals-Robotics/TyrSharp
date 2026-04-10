using System.Numerics;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Optimization;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;

namespace Tyr.Vision.Estimators.Chip;

public class ChipKickNonlinearVelocityFitter
{
    private const double FunctionTolerance = 1e-3;
    private const int MaxIterations = 20;
    private const double SimplexStep = 10.0;

    private readonly Vector2 _kickPosition;
    private readonly Timestamp _kickTimestamp;
    private readonly IReadOnlyDictionary<uint, CameraCalibration> _cameraCalibrations;
    private readonly double[] _kickVelocityGuess;
    private readonly NelderMeadSimplex _optimizer = new(FunctionTolerance, MaxIterations);
    private readonly DenseVector _initialGuessVector = new(3);
    private readonly DenseVector _initialPerturbation = new(3);
    private readonly IObjectiveFunction _objectiveFunction;
    private ChipTrajectoryErrorModel? _currentModel;
    private double _bestVelocityX;
    private double _bestVelocityY;
    private double _bestVelocityZ;
    private double _bestError;

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
        _objectiveFunction = ObjectiveFunction.Value(EvaluateObjective);
    }

    public SolvedKick? Solve(List<RawBall> ballRecords)
    {
        if (ballRecords.Count == 0)
        {
            return null;
        }

        _currentModel = new ChipTrajectoryErrorModel(ballRecords, _kickPosition, _kickTimestamp, _cameraCalibrations);
        _bestVelocityX = _kickVelocityGuess[0];
        _bestVelocityY = _kickVelocityGuess[1];
        _bestVelocityZ = _kickVelocityGuess[2];
        _bestError = double.PositiveInfinity;

        _initialGuessVector[0] = _kickVelocityGuess[0];
        _initialGuessVector[1] = _kickVelocityGuess[1];
        _initialGuessVector[2] = _kickVelocityGuess[2];

        _initialPerturbation[0] = SimplexStep;
        _initialPerturbation[1] = SimplexStep;
        _initialPerturbation[2] = SimplexStep;

        double resultX;
        double resultY;
        double resultZ;
        try
        {
            var result = _optimizer.FindMinimum(_objectiveFunction, _initialGuessVector, _initialPerturbation);
            var point = result.MinimizingPoint;
            resultX = point[0];
            resultY = point[1];
            resultZ = point[2];
        }
        catch (Exception) when (!double.IsPositiveInfinity(_bestError))
        {
            resultX = _bestVelocityX;
            resultY = _bestVelocityY;
            resultZ = _bestVelocityZ;
        }
        catch (Exception)
        {
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

    private double EvaluateObjective(MathNet.Numerics.LinearAlgebra.Vector<double> point)
    {
        var error = _currentModel!.Value(point[0], point[1], point[2]);
        if (error < _bestError)
        {
            _bestError = error;
            _bestVelocityX = point[0];
            _bestVelocityY = point[1];
            _bestVelocityZ = point[2];
        }

        return error;
    }

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
