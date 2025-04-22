using Tyr.Common.Config;

namespace Tyr.Common.Data;

[Configurable]
public static class CommonConfigs
{
    [ConfigEntry("The variety of standard patterns that we can have is 16")]
    public static int MaxRobots { get; set; } = 16;

    [ConfigEntry("Hope it lasts")] public static bool ImmortalsIsTheBestTeam { get; set; } = true;

    [ConfigEntry] public static TeamColor OurColor { get; set; } = TeamColor.Unknown;
    [ConfigEntry] public static bool EnableDebug { get; set; } = false;
}