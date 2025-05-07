using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Math;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Vision.Filter;

namespace Tyr.Vision.Tracking;

[Configurable]
public partial class RobotTracker
{
    [ConfigEntry] private static float InitialCovarianceXy { get; set; } = 100.0f;
    [ConfigEntry] private static float ModelErrorXy { get; set; } = 0.1f;
    [ConfigEntry] private static float MeasurementErrorXy { get; set; } = 20.0f;

    [ConfigEntry] private static float InitialCovarianceW { get; set; } = 100.0f;
    [ConfigEntry] private static float ModelErrorW { get; set; } = 0.1f;
    [ConfigEntry] private static float MeasurementErrorW { get; set; } = 2.0f;

    [ConfigEntry("Maximum assumed robot speed in [mm/s] to filter outliers")]
    private static float MaxLinearVelocity { get; set; } = 6000.0f;

    [ConfigEntry("Maximum assumed angular robot speed in [deg/s] to filter outliers")]
    private static float MaxAngularVelocity { get; set; } = 1700f;

    [ConfigEntry("Reciprocal health is used as uncertainty, increased on update, decreased on prediction")]
    private static int MaxHealth { get; set; } = 20;

    public Filter2D FilterXy { get; }
    public Filter1D FilterW { get; }

    private readonly List<Timestamp> _updateTimestamps = [];

    private int _health = 2;
    public float VisionQuality { get; private set; }
    private long _orientationTurns;

    public RawRobot LastRawRobot { get; private set; }

    public RobotId Id { get; }

    public Camera Camera { get; }

    public Timestamp LastUpdateTimestamp => LastRawRobot.CaptureTimestamp;

    // Reciprocal health is used as uncertainty (low health = high uncertainty)
    public float Uncertainty => 1f / _health;

    public Vector2 GetPosition(Timestamp timestamp) => FilterXy.GetPosition(timestamp);
    public Vector2 Position => FilterXy.Position;
    public Vector2 Velocity => FilterXy.Velocity;

    public Angle GetAngle(Timestamp timestamp) => Angle.FromRad(FilterW.GetPosition(timestamp));
    public Angle Angle => Angle.FromRad(FilterW.Position);
    public Angle AngularVelocity => Angle.FromRad(FilterW.Velocity);

    public RobotTracker(Camera camera, RawRobot raw, TeamColor color)
    {
        Camera = camera;

        FilterXy = new Filter2D(raw.Detection.Position,
            InitialCovarianceXy, ModelErrorXy, MeasurementErrorXy,
            raw.CaptureTimestamp);

        FilterW = new Filter1D(raw.Detection.OrientationRad.GetValueOrDefault(),
            InitialCovarianceW, ModelErrorW, MeasurementErrorW,
            raw.CaptureTimestamp);

        Id = new RobotId { Id = raw.Detection.RobotId, Team = color };
        LastRawRobot = raw;
    }

    public RobotTracker(Camera camera, RawRobot raw, FilteredRobot filtered, TeamColor color)
    {
        Camera = camera;

        FilterXy = new Filter2D(filtered.State.Position, filtered.State.Velocity,
            InitialCovarianceXy, ModelErrorXy, MeasurementErrorXy,
            raw.CaptureTimestamp);

        FilterW = new Filter1D(filtered.State.Angle.Rad, filtered.State.AngularVelocity.Rad,
            InitialCovarianceW, ModelErrorW, MeasurementErrorW,
            raw.CaptureTimestamp);

        Id = new RobotId { Id = raw.Detection.RobotId, Team = color };
        LastRawRobot = raw;
    }

    public void Predict(Timestamp timestamp, DeltaTime averageDt)
    {
        FilterXy.Predict(timestamp);
        FilterW.Predict(timestamp);

        _health = Math.Clamp(_health - 1, 1, MaxHealth);

        _updateTimestamps.RemoveAll(t => timestamp - t > DeltaTime.FromSeconds(1));
        VisionQuality = (float)Math.Clamp(_updateTimestamps.Count * averageDt.Seconds, 0.01, 1);
    }

    // returns whether the robot was valid for updating the tracker
    public bool Update(RawRobot robot)
    {
        var dtInSec = (float)(robot.CaptureTimestamp - LastRawRobot.CaptureTimestamp).Seconds;
        var distanceToPrediction = Vector2.Distance(FilterXy.Position, robot.Detection.Position);
        if (distanceToPrediction > dtInSec * MaxLinearVelocity)
        {
            // measurement too far away => refuse update
            return false;
        }

        var angDiff = Angle.FromRad(FilterW.Position) - robot.Detection.Orientation.GetValueOrDefault();
        if (Math.Abs(angDiff.DegNormalized) > dtInSec * MaxAngularVelocity)
        {
            // orientation mismatch, maybe a +-90° vision switch => refuse update
            return false;
        }

        // we have an update, increase health/certainty in this tracker
        _health = Math.Clamp(_health + 2, 1, MaxHealth);

        FilterXy.Correct(robot.Detection.Position);

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
        FilterW.Correct(orientation);

        _updateTimestamps.Add(robot.CaptureTimestamp);
        LastRawRobot = robot;

        return true;
    }

    public void DrawDebug(Timestamp timestamp)
    {
        var outlineColor = Id.Team == TeamColor.Blue ? Color.Blue200 : Color.Yellow100;
        var textColor = Id.Team == TeamColor.Blue ? Color.Blue200 : Color.Yellow50;

        Draw.DrawRobot(Position, Angle, Id.Id, outlineColor, 120f, Options.Outline());

        var uncertainty = FilterXy.PositionUncertainty.Length() * Uncertainty;
        var behind = timestamp - LastUpdateTimestamp;
        Draw.DrawText($"[{Camera.Id}] unc.: {uncertainty:F2}, dt: {behind.Milliseconds:F2}ms",
            Position + new Vector2(130, Camera.Id * 60), 50f,
            textColor, TextAlignment.BottomLeft);
    }
}