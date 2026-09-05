using System.Numerics;
using Tyr.Common.Config;

namespace Tyr.Sender;

// TODO: this doesn't belong here in Sender, move to Soccer
[Configurable]
public partial class ShootCalibration
{
    public enum ShootType
    {
        Shoot,
        Chip,
    }

    [ConfigEntry]
    private static partial Vector2[] ShootCoeffs { get; set; } =
    [
        new(4.45f, 2.59f), //  0
        new(4.48f, 2.6f), //  1
        new(3.42f, 3.43f), //  2
        new(6.22f, -0.43f), //  3
        new(9.74f, 12.8f), //  4
        new(4.46f, 2.66f), //  5
        new(9.74f, 12.8f), //  6
        new(4.02f, 3.38f), //  7
        new(1.0f, 0.0f), //  8*
        new(1.0f, 0.0f), //  9*
        new(1.0f, 0.0f), // 10*
        new(1.0f, 0.0f), // 11*
        new(1.0f, 0.0f), // 12*
        new(1.0f, 0.0f), // 13*
        new(1.0f, 0.0f), // 14*
        new(1.0f, 0.0f) // 15*
    ];

    [ConfigEntry]
    private static partial Vector2[] ChipCoeffs { get; set; } =
    [
        new(1.00000f, 2.32507f), //  0
        new(0.77461f, 2.81861f), //  1
        new(1.69062f, 1.54292f), //  2
        new(0.90561f, 3.13007f), //  3
        new(1.01367f, 1.74667f), //  4
        new(1.01367f, 1.74667f), //  5
        new(1.45657f, 1.18770f), //  6
        new(1.01367f, 1.74667f), //  7
        new(1.0f, 0.0f), //  8*
        new(1.0f, 0.0f), //  9*
        new(1.0f, 0.0f), // 10*
        new(1.0f, 0.0f), // 11*
        new(1.0f, 0.0f), // 12*
        new(1.0f, 0.0f), // 13*
        new(1.0f, 0.0f), // 14*
        new(1.0f, 0.0f) // 15*
    ];

    public static float GetCalibratedPower(float rawShoot, ShootType type, int robotId)
    {
        if (rawShoot <= 0)
        {
            return 0;
        }

        var coeffs = type switch
        {
            ShootType.Shoot => ShootCoeffs[robotId],
            ShootType.Chip => ChipCoeffs[robotId],
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, null)
        };

        var calibrated = coeffs.X * rawShoot + coeffs.Y;
        return Math.Clamp(calibrated, 0f, 100f);
    }
}