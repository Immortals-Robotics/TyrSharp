using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Vision;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Vision.Data;
using Tyr.Vision.Filter;

namespace Tyr.Vision.Tracker;

[Configurable]
public partial class Robot
{
    [ConfigEntry] private static float InitialCovarianceXy { get; set; } = 100.0f;
    [ConfigEntry] private static float ModelErrorXy { get; set; } = 0.1f;
    [ConfigEntry] private static float MeasurementErrorXy { get; set; } = 20.0f;

    [ConfigEntry] private static float InitialCovarianceW { get; set; } = 100.0f;
    [ConfigEntry] private static float ModelErrorW { get; set; } = 0.1f;
    [ConfigEntry] private static float MeasurementErrorW { get; set; } = 2.0f;

    [ConfigEntry(
        "Factor to weight stdDeviation during tracker merging, reasonable range: 1.0 - 2.0. High values lead to more jitter")]
    private static float MergePower { get; set; } = 1.5f;

    [ConfigEntry("Maximum assumed robot speed in [mm/s] to filter outliers")]
    private static float MaxLinearVelocity { get; set; } = 6000.0f;

    [ConfigEntry("Maximum assumed angular robot speed in [deg/s] to filter outliers")]
    private static float MaxAngularVelocity { get; set; } = 1700f;

    [ConfigEntry("Reciprocal health is used as uncertainty, increased on update, decreased on prediction")]
    private static int MaxHealth { get; set; } = 20;

    private readonly Filter2D _filterXy;
    private readonly Filter1D _filterW;

    private readonly List<Timestamp> _updateTimestamps = [];

    private int _health = 2;
    private float _visionQuality;
    private long _orientationTurns;

    public RawRobot LastRawRobot { get; private set; }

    public RobotId Id { get; }

    public uint CameraId => LastRawRobot.CameraId;

    public Timestamp LastUpdateTimestamp => LastRawRobot.CaptureTimestamp;

    // Reciprocal health is used as uncertainty (low health = high uncertainty)
    public float Uncertainty => 1f / _health;

    public Vector2 GetPosition(Timestamp timestamp) => _filterXy.GetPosition(timestamp);
    public Vector2 Position => _filterXy.Position;
    public Vector2 Velocity => _filterXy.Velocity;

    public Angle GetAngle(Timestamp timestamp) => Angle.FromRad(_filterW.GetPosition(timestamp));
    public Angle Angle => Angle.FromRad(_filterW.Position);
    public Angle AngularVelocity => Angle.FromRad(_filterW.Velocity);

    public Robot(RawRobot raw, TeamColor color)
    {
        _filterXy = new Filter2D(raw.Detection.Position,
            InitialCovarianceXy, ModelErrorXy, MeasurementErrorXy,
            raw.CaptureTimestamp);

        _filterW = new Filter1D(raw.Detection.OrientationRad.GetValueOrDefault(),
            InitialCovarianceW, ModelErrorW, MeasurementErrorW,
            raw.CaptureTimestamp);

        Id = new RobotId { Id = raw.Detection.RobotId, Team = color };
        LastRawRobot = raw;
    }

    public Robot(RawRobot raw, FilteredRobot filtered, TeamColor color)
    {
        _filterXy = new Filter2D(filtered.State.Position, filtered.State.Velocity,
            InitialCovarianceXy, ModelErrorXy, MeasurementErrorXy,
            raw.CaptureTimestamp);

        _filterW = new Filter1D(filtered.State.Angle.Rad, filtered.State.AngularVelocity.Rad,
            InitialCovarianceW, ModelErrorW, MeasurementErrorW,
            raw.CaptureTimestamp);

        Id = new RobotId { Id = raw.Detection.RobotId, Team = color };
        LastRawRobot = raw;
    }

    public void Predict(Timestamp timestamp, DeltaTime averageDt)
    {
        _filterXy.Predict(timestamp);
        _filterW.Predict(timestamp);

        _health = Math.Clamp(_health - 1, 1, MaxHealth);

        _updateTimestamps.RemoveAll(t => timestamp - t > DeltaTime.FromSeconds(1));
        _visionQuality = (float)Math.Clamp(_updateTimestamps.Count * averageDt.Seconds, 0.01, 1);
    }

    // returns whether the robot was valid for updating the tracker
    public bool Update(RawRobot robot)
    {
        var dtInSec = (float)(robot.CaptureTimestamp - LastRawRobot.CaptureTimestamp).Seconds;
        var distanceToPrediction = Vector2.Distance(_filterXy.Position, robot.Detection.Position);
        if (distanceToPrediction > dtInSec * MaxLinearVelocity)
        {
            // measurement too far away => refuse update
            return false;
        }

        var angDiff = Angle.FromRad(_filterW.Position) - robot.Detection.Orientation.GetValueOrDefault();
        if (Math.Abs(angDiff.DegNormalized) > dtInSec * MaxAngularVelocity)
        {
            // orientation mismatch, maybe a +-90° vision switch => refuse update
            return false;
        }

        // we have an update, increase health/certainty in this tracker
        _health = Math.Clamp(_health + 2, 1, MaxHealth);

        _filterXy.Correct(robot.Detection.Position);

        var orientation = robot.Detection.OrientationRad.GetValueOrDefault();

        // multi-turn angle correction
        var lastOrientation = LastRawRobot.Detection.OrientationRad.GetValueOrDefault();
        switch (orientation)
        {
            case < -MathF.PI / 2f when lastOrientation > MathF.PI / 2f:
                _orientationTurns += 1;
                break;
            case > MathF.PI / 2f when lastOrientation < -MathF.PI / 2f:
                _orientationTurns -= 1;
                break;
        }

        orientation += _orientationTurns * 2f * MathF.PI;
        _filterW.Correct(orientation);

        _updateTimestamps.Add(robot.CaptureTimestamp);
        LastRawRobot = robot;

        return true;
    }

    private float PositionUncertaintyWeight =>
        MathF.Pow(_filterXy.PositionUncertainty.Length() * Uncertainty, -MergePower);

    private float VelocityUncertaintyWeight =>
        MathF.Pow(_filterXy.VelocityUncertainty.Length() * Uncertainty, -MergePower);

    private float OrientationUncertaintyWeight =>
        MathF.Pow(_filterW.PositionUncertainty * Uncertainty, -MergePower);

    private float AngularVelocityUncertaintyWeight =>
        MathF.Pow(_filterW.VelocityUncertainty * Uncertainty, -MergePower);

    public static FilteredRobot Merge(IReadOnlyList<Robot> trackers, Timestamp timestamp)
    {
        Assert.IsNotEmpty(trackers);

        var totalPositionUncertainty = 0f;
        var totalVelocityUncertainty = 0f;
        var totalOrientationUncertainty = 0f;
        var totalAngularVelocityUncertainty = 0f;

        var maxQuality = 0f;

        // calculate sum of all uncertainties
        foreach (var tracker in trackers)
        {
            totalPositionUncertainty += tracker.PositionUncertaintyWeight;
            totalVelocityUncertainty += tracker.VelocityUncertaintyWeight;
            totalOrientationUncertainty += tracker.OrientationUncertaintyWeight;
            totalAngularVelocityUncertainty += tracker.AngularVelocityUncertaintyWeight;

            maxQuality = MathF.Max(maxQuality, tracker._visionQuality);
        }

        Assert.IsPositive(totalPositionUncertainty);
        Assert.IsPositive(totalVelocityUncertainty);
        Assert.IsPositive(totalOrientationUncertainty);
        Assert.IsPositive(totalAngularVelocityUncertainty);

        var position = Vector2.Zero;
        var velocity = Vector2.Zero;
        var orientation = 0f;
        var angularVelocity = 0f;

        // cyclic coordinates don't like mean calculations, we will work with offsets though
        // TODO: probably better to use the median as the offset
        var orientationOffset = trackers[0].GetAngle(timestamp);

        // take all trackers and calculate their pos/vel sum weighted by uncertainty.
        // Trackers with high uncertainty have less influence on the merged result.
        foreach (var tracker in trackers)
        {
            var positionWeight = tracker.PositionUncertaintyWeight;
            position += tracker.GetPosition(timestamp) * positionWeight;

            velocity += tracker.Velocity * tracker.VelocityUncertaintyWeight;

            orientation += (tracker.GetAngle(timestamp) - orientationOffset).Rad * tracker.OrientationUncertaintyWeight;

            angularVelocity += tracker.AngularVelocity.Rad * tracker.AngularVelocityUncertaintyWeight;
        }

        position /= totalPositionUncertainty;
        velocity /= totalVelocityUncertainty;
        orientation /= totalOrientationUncertainty;
        angularVelocity /= totalAngularVelocityUncertainty;

        var state = new RobotState
        {
            Position = position,
            Velocity = velocity,
            Angle = Angle.FromRad(orientation),
            AngularVelocity = Angle.FromRad(angularVelocity),
        };

        return new FilteredRobot
        {
            Id = trackers[0].Id, // TODO: verify that all trackers have the same ID
            Timestamp = timestamp,
            State = state,
            Quality = maxQuality,
        };
    }
}