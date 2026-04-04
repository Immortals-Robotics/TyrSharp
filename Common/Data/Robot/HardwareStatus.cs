namespace Tyr.Common.Data.Robot;

/// <summary>
/// Accumulated hardware status built by merging StatusUpdate messages over time.
/// Each Apply() call overwrites only the sub-status that arrived; the rest stay at
/// their last known value.
/// </summary>
public class HardwareStatus
{
    public uint            RobotId { get; private set; }
    public ImuStatus?      Imu     { get; private set; }
    public MotorStatus?    Motors  { get; private set; }
    public PowerStatus?    Power   { get; private set; }
    public MikonaStatus?   Mikona  { get; private set; }
    public RobotInfo?      Info    { get; private set; }
    public DiagStatus?     Diag    { get; private set; }

    /// <summary>
    /// Applies one StatusUpdate, overwriting whichever sub-status it carries.
    /// Returns true if the RobotId changed (caller should re-key the dictionary).
    /// </summary>
    public bool Apply(StatusUpdate update)
    {
        if (update.Imu    is { } imu)    Imu    = imu;
        if (update.Motors is { } motors) Motors = motors;
        if (update.Power  is { } power)  Power  = power;
        if (update.Mikona is { } mikona) Mikona = mikona;
        if (update.Diag   is { } diag)   Diag   = diag;

        if (update.Info is { } info)
        {
            Info = info;
            if (info.RobotId == RobotId) return false;
            RobotId = info.RobotId;
            return true;
        }

        return false;
    }
}
