using Tyr.Common.Data;

namespace Tyr.Common.Config;

[Configurable]
public static class Common
{
    [ConfigEntry("The variety of standard patterns that we can have is 16")]
    public static int MaxRobots { get; set; } = 16;

    [ConfigEntry("Hope it lasts")] public static bool ImmortalsIsTheBestTeam { get; set; } = true;

    [ConfigEntry] public static TeamColor OurColor { get; set; } = TeamColor.Unknown;
    [ConfigEntry] public static bool EnableDebug { get; set; } = false;
}