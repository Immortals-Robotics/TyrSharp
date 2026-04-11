namespace Tyr.Common.Data.Robot;

// Internal wrapper produced after ZMQ topic dispatch.
// Exactly one field is non-null per instance, matching the StatusTopic byte.
public record StatusUpdate
{
    public int                     RobotId { get; init; }
    public ImuStatus?              Imu              { get; init; }
    public MotorStatus?            Motors           { get; init; }
    public PowerStatus?            Power            { get; init; }
    public MikonaStatus?           Mikona           { get; init; }
    public RobotInfo?              Info             { get; init; }
    public DiagStatus?             Diag             { get; init; }
    public IrSensorStatus?         IrSensor         { get; init; }
    public DribblerFeedbackStatus? DribblerFeedback { get; init; }
}
