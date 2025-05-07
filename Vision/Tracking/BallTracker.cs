using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Filter;
using Rectangle = Tyr.Common.Math.Shapes.Rectangle;

namespace Tyr.Vision.Tracking;

[Configurable]
public partial class BallTracker
{
    [ConfigEntry] private static float InitialCovariance { get; set; } = 1000f;
    [ConfigEntry] private static float ModelError { get; set; } = 0.1f;
    [ConfigEntry] private static float MeasurementError { get; set; } = 100.0f;

    [ConfigEntry("Maximum assumed ball speed in [mm/s] to filter outliers")]
    public static float MaxLinearVelocity { get; private set; } = 15000f;

    [ConfigEntry("Reciprocal health is used as uncertainty, increased on update, decreased on prediction")]
    private static int MaxHealth { get; set; } = 20;

    [ConfigEntry("How many updates are required until this tracker is grown up?")]
    private static int GrownUpAge { get; set; } = 3;

    public Filter2D Filter { get; }

    private int _health = 2;
    private int _age;

    public RawBall LastRawBall { get; private set; }
    public bool Updated { get; set; }

    public float? MaxDistance { get; set; }

    // Reciprocal health is used as uncertainty (low health = high uncertainty)
    public float Uncertainty => 1f / _health;
    public bool IsGrownUp => _age >= GrownUpAge;

    public Timestamp LastUpdateTimestamp => LastRawBall.CaptureTimestamp;
    public Timestamp LastInFieldTimestamp { get; private set; }

    public Camera Camera { get; }

    public BallTracker(Camera camera, RawBall ball)
    {
        Camera = camera;

        Filter = new Filter2D(ball.Detection.Position,
            InitialCovariance, ModelError, MeasurementError,
            ball.CaptureTimestamp);

        LastInFieldTimestamp = ball.CaptureTimestamp;
        LastRawBall = ball;
    }

    public BallTracker(Camera camera, RawBall rawBall, FilteredBall filteredBall)
    {
        Camera = camera;

        var velocity = filteredBall.State.Velocity.Xy()
            .ClampMagnitude(0f, MaxLinearVelocity);

        Filter = new Filter2D(rawBall.Detection.Position, velocity,
            InitialCovariance, ModelError, MeasurementError,
            rawBall.CaptureTimestamp);

        LastInFieldTimestamp = rawBall.CaptureTimestamp;
        LastRawBall = rawBall;
    }

    public void Predict(Timestamp timestamp)
    {
        Filter.Predict(timestamp);

        _health = Math.Clamp(_health - 1, 1, MaxHealth);
    }

    // returns whether the ball was valid for updating the tracker
    public bool Update(RawBall ball, Rectangle? field)
    {
        // calculate delta time since last update
        var dt = ball.CaptureTimestamp - LastRawBall.CaptureTimestamp;
        Assert.IsPositive(dt.Nanoseconds);

        // calculate distance of this ball to our internal prediction
        var distanceToPrediction = Vector2.Distance(Filter.Position, ball.Detection.Position);

        // ignore the ball if it is too far away from our prediction...
        var maxAllowedDistance = Math.Min(
            dt.Seconds * MaxLinearVelocity, // distance based on the assumed max ball speed
            MaxDistance.GetValueOrDefault(float.MaxValue)); // a hard limit
        if (distanceToPrediction > maxAllowedDistance) return false;

        // we have an update, increase health/certainty in this tracker
        _health = Math.Clamp(_health + 2, 1, MaxHealth);
        _age = Math.Clamp(_age + 1, 0, GrownUpAge);

        // run correction on tracking filter
        Filter.Correct(ball.Detection.Position);

        // if we know the field size, check if the ball is inside it
        if (field is not { } rectangle || rectangle.Inside(ball.Detection.Position))
        {
            LastInFieldTimestamp = ball.CaptureTimestamp;
        }

        // store raw ball for next run
        LastRawBall = ball;
        Updated = true;

        return true;
    }
}