using System.Numerics;
using Tyr.Common.Data.Ssl.Vision.Detection;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Estimators.Direct;
using Tyr.Vision.Trajectory;
using RawBall = Tyr.Vision.Data.RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;

namespace Tyr.Tests.Vision.Estimators.Direct;

public class RedirectKickSpinAwareFitterTests
{
    private const float RedirectSpinFactor = 0.8f;

    [Fact]
    public void Solve_FitsRedirectTrajectory_WhenInboundSpinIndicatesRedirect()
    {
        var expectedPosition = new Vector2(200f, -50f);
        var expectedDirection = Vector2.Normalize(new Vector2(3f, 4f));
        var expectedSpeed = 3000f;
        var inboundSpin = expectedDirection * 120f;
        var expectedSpin = inboundSpin * RedirectSpinFactor;
        var firstTimestampSeconds = 5.0;

        var records = CreateFlatTrajectoryRecords(
            expectedPosition,
            expectedDirection * expectedSpeed,
            expectedSpin,
            firstTimestampSeconds,
            [0.0, 0.04, 0.08, 0.12, 0.16, 0.20, 0.30]);

        var fitter = new RedirectKickSpinAwareFitter(
            Angle.Zero,
            new FilteredBall
            {
                State = new BallState
                {
                    SpinRadians = inboundSpin
                }
            });

        var solved = fitter.Solve(records);

        Assert.NotNull(solved);
        var solvedVelocity = new Vector2(solved!.Velocity.X, solved.Velocity.Y);
        var positionError = Vector2.Distance(solved.Position, expectedPosition);
        var velocityError = Vector2.Distance(solvedVelocity, expectedDirection * expectedSpeed);
        var spinError = Vector2.Distance(solved.Spin, expectedSpin);

        Assert.True(
            positionError < 0.75f && velocityError < 10f && spinError < 0.5f,
            $"Position error: {positionError:F3}, velocity error: {velocityError:F3}, spin error: {spinError:F3}, " +
            $"solved position: {solved.Position}, solved velocity: {solvedVelocity}, solved spin: {solved.Spin}");
        Assert.Equal(nameof(RedirectKickSpinAwareFitter), solved.Name);
    }

    [Fact]
    public void Solve_ReturnsNull_WhenInboundSpinIsTooSmallForRedirect()
    {
        var records = CreateFlatTrajectoryRecords(
            new Vector2(100f, 30f),
            new Vector2(2000f, 0f),
            Vector2.Zero,
            1.0,
            [0.0, 0.03, 0.06, 0.09]);

        var fitter = new RedirectKickSpinAwareFitter(
            Angle.Zero,
            new FilteredBall
            {
                State = new BallState
                {
                    SpinRadians = new Vector2(10f, 0f)
                }
            });

        Assert.Null(fitter.Solve(records));
    }

    [Fact]
    public void Solve_ReturnsNull_WhenDirectionCannotBeEstimated()
    {
        var records = new List<RawBall>
        {
            CreateRawBall(new Vector2(100f, 40f), 1.00, 1),
            CreateRawBall(new Vector2(100f, 40f), 1.02, 2),
            CreateRawBall(new Vector2(100f, 40f), 1.04, 3)
        };

        var fitter = new RedirectKickSpinAwareFitter(
            Angle.Zero,
            new FilteredBall
            {
                State = new BallState
                {
                    SpinRadians = new Vector2(100f, 0f)
                }
            });

        Assert.Null(fitter.Solve(records));
    }

    private static List<RawBall> CreateFlatTrajectoryRecords(
        Vector2 initialPosition,
        Vector2 initialVelocity,
        Vector2 spin,
        double firstTimestampSeconds,
        params double[] sampleOffsetsSeconds)
    {
        var trajectory = new BallFlat(new BallState
        {
            Position3D = new Vector3(initialPosition.X, initialPosition.Y, 0f),
            Velocity = new Vector3(initialVelocity.X, initialVelocity.Y, 0f),
            Acceleration = Vector3.Zero,
            SpinRadians = spin
        });

        return sampleOffsetsSeconds
            .Select((offset, index) =>
            {
                var position = trajectory.GetState(DeltaTime.FromSeconds(offset)).Position;
                return CreateRawBall(position, firstTimestampSeconds + offset, (uint)(index + 1));
            })
            .ToList();
    }

    private static RawBall CreateRawBall(Vector2 position, double captureTimeSeconds, uint frameNumber)
    {
        var frame = new Frame
        {
            CameraId = 1,
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
