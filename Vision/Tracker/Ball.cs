using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;
using Tyr.Vision.Filter;

namespace Tyr.Vision.Tracker;

using RawBall = RawDetection<Tyr.Common.Data.Ssl.Vision.Detection.Ball>;
using FilteredBall = Common.Data.Ssl.Vision.Tracker.Ball;

[Configurable]
public class Ball
{
    [ConfigEntry] private static float InitialCovarianceXy { get; set; } = 1000f;
    [ConfigEntry] private static float ModelError { get; set; } = 0.1f;
    [ConfigEntry] private static float MeasurementError { get; set; } = 100.0f;

    [ConfigEntry("Maximum assumed ball speed in [mm/s] to filter outliers")]
    private static float MaxLinearVelocity { get; set; } = 15000f;

    [ConfigEntry(
        "Factor to weight stdDeviation during tracker merging, reasonable range: 1.0 - 2.0. High values lead to more jitter")]
    private static float MergePower { get; set; } = 1.5f;

    [ConfigEntry("Reciprocal health is used as uncertainty, increased on update, decreased on prediction")]
    private static int MaxHealth { get; set; } = 20;

    [ConfigEntry("How many updates are required until this tracker is grown up?")]
    private static int GrownUpAge { get; set; } = 3;

    public Filter2D Filter { get; }

    private int _health = 2;
    private int _age;

    public RawBall LastRawBall { get; private set; }
    private bool _updated = false;

    public float? MaxDistance { get; set; }

    public float Uncertainty => 1f / _health;
    public bool IsGrownUp => _age >= GrownUpAge;

    public DateTime LastUpDateTime => LastRawBall.CaptureTime;
    public DateTime LastInFieldTime { get; private set; }

    public uint CameraId => LastRawBall.CameraId;

    public Vector2 GetPosition(DateTime time) => Filter.GetPosition(time);
    public Vector2 Position => Filter.Position;
    public Vector2 Velocity => Filter.Velocity;

    public Ball(RawBall ball)
    {
        Filter = new Filter2D(ball.Detection.Position,
            InitialCovarianceXy, ModelError, MeasurementError,
            ball.CaptureTime);

        LastInFieldTime = ball.CaptureTime;
        LastRawBall = ball;
    }

    public Ball(RawBall rawBall, FilteredBall filteredBall)
    {
        var velocity = filteredBall.Velocity.GetValueOrDefault().Xy()
            .ClampMagnitude(0f, MaxLinearVelocity);

        Filter = new Filter2D(rawBall.Detection.Position, velocity,
            InitialCovarianceXy, ModelError, MeasurementError,
            rawBall.CaptureTime);

        LastInFieldTime = rawBall.CaptureTime;
        LastRawBall = rawBall;
    }

    public void Predict(DateTime time)
    {
        Filter.Predict(time);

        if (_health > 1)
        {
            _health--;
        }
    }

    // returns whether the ball was valid for updating the tracker
    public bool Update(RawBall ball, Rectangle? field)
    {
        // calculate delta time since last update
        var dt = (ball.CaptureTime - LastRawBall.CaptureTime).TotalSeconds;

        // calculate distance of this ball to our internal prediction
        var distanceToPrediction = Vector2.Distance(Filter.Position, ball.Detection.Position);

        // ignore the ball if it is too far away from our prediction...
        var maxAllowedDistance = Math.Min(
            (float)(dt * MaxLinearVelocity), // distance based on the assumed max ball speed
            MaxDistance.GetValueOrDefault(float.MaxValue)); // a hard limit
        if (distanceToPrediction > maxAllowedDistance) return false;

        // we have an update, increase health/certainty in this tracker
        _health = Math.Clamp(_health + 2, 0, MaxHealth);
        _age = Math.Clamp(_age + 1, 0, GrownUpAge);

        // run correction on tracking filter
        Filter.Correct(ball.Detection.Position);

        // if we know the field size, check if the ball is inside it
        if (!field.HasValue || field.Value.Inside(ball.Detection.Position))
        {
            LastInFieldTime = ball.CaptureTime;
        }

        // store cam ball for next run
        LastRawBall = ball;
        _updated = true;

        return true;
    }
}