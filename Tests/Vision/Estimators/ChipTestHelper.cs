using System.Numerics;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Data;
using Tyr.Vision.Trajectory;
using Tyr.Vision.Util;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators;

internal static class ChipTestHelper
{
    public static IReadOnlyDictionary<uint, CameraCalibration> CreateCalibrations(
        params (uint CameraId, Vector3 CameraPosition)[] cameras)
    {
        return cameras.ToDictionary(
            camera => camera.CameraId,
            camera => new CameraCalibration
            {
                CameraId = camera.CameraId,
                DerivedCameraWorldTx = camera.CameraPosition.X,
                DerivedCameraWorldTy = camera.CameraPosition.Y,
                DerivedCameraWorldTz = camera.CameraPosition.Z
            });
    }

    public static List<RawBall> CreateChipTrajectoryRecords(
        Vector2 kickPosition,
        Vector3 kickVelocity,
        IReadOnlyDictionary<uint, CameraCalibration> calibrations,
        double firstTimestampSeconds,
        params (uint CameraId, double OffsetSeconds)[] samples)
    {
        var trajectory = BallChip.FromKick(kickPosition, kickVelocity, Vector2.Zero);

        return samples
            .Select((sample, index) =>
            {
                var state = trajectory.GetState(DeltaTime.FromSeconds(sample.OffsetSeconds));
                var ground = BallProjection.ProjectToGround(
                    state.Position3D,
                    calibrations[sample.CameraId].DerivedCameraWorld);
                return CreateRawBall(sample.CameraId, ground, firstTimestampSeconds + sample.OffsetSeconds, (uint)(index + 1));
            })
            .OrderBy(ball => ball.CaptureTimestamp)
            .ThenBy(ball => ball.CameraId)
            .ThenBy(ball => ball.FrameNumber)
            .ToList();
    }

    public static DetectedKick CreateDetectedKick(
        IReadOnlyList<RawBall> records,
        Vector2 kickPosition,
        Vector2 kickDirection,
        bool isFastDetection = false,
        Timestamp? kickTimestamp = null)
    {
        var mergedBalls = records
            .Select(record => new MergedBall
            {
                Position = record.Detection.Position,
                RawPosition = record.Detection.Position,
                Timestamp = record.CaptureTimestamp,
                LatestRawBall = record
            })
            .ToList();

        return new DetectedKick(
            new RobotId { Id = 1 },
            new RobotState
            {
                Position = kickPosition - (kickDirection * 90f),
                Velocity = Vector2.Zero,
                Angle = Angle.FromVector(kickDirection),
                AngularVelocity = Angle.Zero
            },
            kickPosition,
            kickTimestamp ?? records[0].CaptureTimestamp,
            isFastDetection,
            mergedBalls);
    }

    public static FilteredBall CreateFilteredBall(RawBall record)
    {
        return new FilteredBall
        {
            Timestamp = record.CaptureTimestamp,
            LastVisibleTimestamp = record.CaptureTimestamp,
            State = new BallState
            {
                Position3D = new Vector3(record.Detection.Position.X, record.Detection.Position.Y, 0f),
                Velocity = Vector3.Zero,
                Acceleration = Vector3.Zero,
                SpinRadians = Vector2.Zero
            }
        };
    }

    public static MergedBall CreateMergedBall(RawBall record)
    {
        return new MergedBall
        {
            Position = record.Detection.Position,
            RawPosition = record.Detection.Position,
            Velocity = Vector2.Zero,
            Timestamp = record.CaptureTimestamp,
            LatestRawBall = record
        };
    }

    public static FilteredRobot CreateRobot(Vector2 position)
    {
        return new FilteredRobot
        {
            Id = new RobotId { Id = 1 },
            Timestamp = Timestamp.Zero,
            State = new RobotState
            {
                Position = position,
                Velocity = Vector2.Zero,
                Angle = Angle.Zero,
                AngularVelocity = Angle.Zero
            },
            Quality = 1f
        };
    }

    public static RawBall CreateRawBall(uint cameraId, Vector2 position, double captureTimeSeconds, uint frameNumber)
    {
        var frame = new Frame
        {
            CameraId = cameraId,
            FrameNumber = frameNumber,
            CaptureTimeSeconds = captureTimeSeconds,
            SentTimeSeconds = captureTimeSeconds
        };

        return new RawBall(new Ball
        {
            Confidence = 1f,
            X = position.X,
            Y = position.Y,
            PixelX = 0f,
            PixelY = 0f
        }, frame);
    }
}
