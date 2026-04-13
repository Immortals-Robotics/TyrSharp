using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Soccer.Helpers;

[Configurable]
public static partial class BallControl
{
    [ConfigEntry("Kicker-to-ball distance below which the dribbler is enabled [mm]")]
    private static float NearKickerDistanceMm { get; set; } = 200f;

    [ConfigEntry("Dribbler speed used when the ball is near the kicker")]
    private static float NearKickerDribblerSpeed { get; set; } = 2f;

    [ConfigEntry("Dribbler force used when the ball is near the kicker")]
    private static float NearKickerDribblerForce { get; set; } = 1f;

    public static bool TryEnableDribblerWhenBallNearKicker(Robot.Robot robot, Vector2 ballPosition)
    {
        var kickerPos = Tyr.Common.Math.Shapes.Robot.GetKickerCenterPos(
            robot.Position,
            robot.Angle,
            robot.CenterToDribbler + Context.Field.BallRadius);

        var isBallNearKicker = Vector2.Distance(kickerPos, ballPosition) < NearKickerDistanceMm;
        if (isBallNearKicker)
        {
            robot.SetDribbler(NearKickerDribblerSpeed, NearKickerDribblerForce);
        }

        return isBallNearKicker;
    }
}
