using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Math.Shapes;
using Tyr.Vision.Filter;

namespace Tyr.Vision.Tracker;

[Configurable]
public class Ball
{
    [ConfigEntry] private static float InitialCovariance { get; set; } = 1000f;
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
    public bool Updated { get; private set; }

    public float? MaxDistance { get; set; }

    // Reciprocal health is used as uncertainty (low health = high uncertainty)
    public float Uncertainty => 1f / _health;
    public bool IsGrownUp => _age >= GrownUpAge;

    public DateTime LastUpdateTime => LastRawBall.CaptureTime;
    public DateTime LastInFieldTime { get; private set; }

    public uint CameraId => LastRawBall.CameraId;

    public Vector2 GetPosition(DateTime time) => Filter.GetPosition(time);
    public Vector2 Position => Filter.Position;
    public Vector2 Velocity => Filter.Velocity;

    public Ball(RawBall ball)
    {
        Filter = new Filter2D(ball.Detection.Position,
            InitialCovariance, ModelError, MeasurementError,
            ball.CaptureTime);

        LastInFieldTime = ball.CaptureTime;
        LastRawBall = ball;
    }

    public Ball(RawBall rawBall, FilteredBall filteredBall)
    {
        var velocity = filteredBall.Velocity.GetValueOrDefault().Xy()
            .ClampMagnitude(0f, MaxLinearVelocity);

        Filter = new Filter2D(rawBall.Detection.Position, velocity,
            InitialCovariance, ModelError, MeasurementError,
            rawBall.CaptureTime);

        LastInFieldTime = rawBall.CaptureTime;
        LastRawBall = rawBall;
    }

    public void Predict(DateTime time)
    {
        Filter.Predict(time);

        _health = Math.Clamp(_health - 1, 1, MaxHealth);
    }

    // returns whether the ball was valid for updating the tracker
    public bool Update(RawBall ball, Rectangle? field)
    {
        // calculate delta time since last update
        var dt = (float)(ball.CaptureTime - LastRawBall.CaptureTime).TotalSeconds;
        Assert.IsPositive(dt);

        // calculate distance of this ball to our internal prediction
        var distanceToPrediction = Vector2.Distance(Filter.Position, ball.Detection.Position);

        // ignore the ball if it is too far away from our prediction...
        var maxAllowedDistance = Math.Min(
            dt * MaxLinearVelocity, // distance based on the assumed max ball speed
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
            LastInFieldTime = ball.CaptureTime;
        }

        // store raw ball for next run
        LastRawBall = ball;
        Updated = true;

        return true;
    }

    private float PositionUncertaintyWeight =>
        MathF.Pow(Filter.PositionUncertainty.Length() * Uncertainty, -MergePower);

    private float VelocityUncertaintyWeight =>
        MathF.Pow(Filter.VelocityUncertainty.Length() * Uncertainty, -MergePower);

    // Merges multiple ball trackers into a single merged ball,
    // weighted by their state uncertainty (less certain = less influence).
    public static MergedBall Merge(IReadOnlyList<Ball> trackers, DateTime time)
    {
        Assert.IsNotEmpty(trackers);

        var totalPositionUncertainty = 0f;
        var totalVelocityUncertainty = 0f;

        RawBall? lastRawBall = null;

        // calculate sum of all uncertainty weights
        foreach (var tracker in trackers)
        {
            totalPositionUncertainty += tracker.PositionUncertaintyWeight;
            totalVelocityUncertainty += tracker.VelocityUncertaintyWeight;

            if (tracker.Updated)
            {
                tracker.Updated = false; // TODO: move this out of this function
                lastRawBall = tracker.LastRawBall;
            }
        }

        Assert.IsPositive(totalPositionUncertainty);
        Assert.IsPositive(totalVelocityUncertainty);

        var position = Vector2.Zero;
        var positionRaw = Vector2.Zero;
        var velocity = Vector2.Zero;

        // take all trackers and calculate their pos/vel sum weighted by uncertainty.
        // Trackers with high uncertainty have less influence on the merged result.
        foreach (var tracker in trackers)
        {
            var positionWeight = tracker.PositionUncertaintyWeight;
            positionRaw += tracker.LastRawBall.Detection.Position * positionWeight;
            position += tracker.GetPosition(time) * positionWeight;

            var velocityWeight = tracker.VelocityUncertaintyWeight;
            velocity += tracker.Velocity * velocityWeight;
        }

        positionRaw /= totalPositionUncertainty;
        position /= totalPositionUncertainty;
        velocity /= totalVelocityUncertainty;

        return new MergedBall
        {
            Position = position,
            RawPosition = positionRaw,
            Velocity = velocity,
            Time = time,
            LatestRawBall = lastRawBall,
        };
    }
}