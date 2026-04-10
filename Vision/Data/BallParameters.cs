using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision.Geometry;

namespace Tyr.Vision.Data;

[Configurable]
internal static partial class BallParameters
{
    [ConfigEntry("Radius of the ball in [mm]")]
    internal static float Radius { get; set; } = 21f;

    [ConfigEntry("Use ball radius and ball model parameters from SSL-Vision geometry when available")]
    internal static bool UseVisionBallParameters { get; set; } = false;

    [ConfigEntry("Sliding acceleration in [mm/s^2], expected to be negative")]
    internal static float AccelerationSlide { get; set; } = -3000f;

    [ConfigEntry("Rolling acceleration in [mm/s^2], expected to be negative")]
    internal static float AccelerationRoll { get; set; } = -260f;

    [ConfigEntry("Fraction of the initial velocity where the ball starts to roll")]
    internal static float KSwitch { get; set; } = 0.64f;

    [ConfigEntry("Ball inertia distribution between 0.4 (massive sphere) and 0.66 (hollow sphere)")]
    internal static float InertiaDistribution { get; set; } = 0.5f;

    [ConfigEntry("Amount of spin transferred during a redirect.")]
    internal static float RedirectSpinFactor { get; set; } = 0.8f;

    [ConfigEntry("Restitution coefficient for redirected balls from a bot.")]
    internal static float RedirectRestitutionCoefficient { get; set; } = 0.2f;

    [ConfigEntry("Chip kick velocity damping factor in XY direction for the first hop")]
    internal static float ChipDampingXyFirstHop { get; set; } = 0.8f;

    [ConfigEntry("Chip kick velocity damping factor in XY direction for all following hops")]
    internal static float ChipDampingXyOtherHops { get; set; } = 0.85f;

    [ConfigEntry("Chip kick velocity damping factor in Z direction")]
    internal static float ChipDampingZ { get; set; } = 0.47f;

    [ConfigEntry("If a chipped ball does not reach this height it is considered rolling [mm]")]
    internal static float MinHopHeight { get; set; } = 10f;

    [ConfigEntry("Max. ball height that can be intercepted by robots [mm]")]
    internal static float MaxInterceptableHeight { get; set; } = 150f;

    internal static void Apply(FieldSize fieldSize)
    {
        if (!UseVisionBallParameters)
        {
            return;
        }

        Radius = fieldSize.BallRadius;
    }

    internal static void Apply(BallModels models)
    {
        if (!UseVisionBallParameters)
        {
            return;
        }

        if (models.StraightTwoPhase.HasValue)
        {
            var straight = models.StraightTwoPhase.Value;
            AccelerationSlide = (float)(straight.AccSlide * 1000.0);
            AccelerationRoll = (float)(straight.AccRoll * 1000.0);
            KSwitch = (float)straight.KSwitch;
        }

        if (models.ChipFixedLoss.HasValue)
        {
            var chip = models.ChipFixedLoss.Value;
            ChipDampingXyFirstHop = (float)chip.DampingXyFirstHop;
            ChipDampingXyOtherHops = (float)chip.DampingXyOtherHops;
            ChipDampingZ = (float)chip.DampingZ;
        }
    }
}
