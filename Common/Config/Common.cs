namespace Tyr.Common.Config;

public class Common
{
    // The variety of standard patterns that we can have is 16
    public const int MaxRobots = 16;

    public bool ImmortalsIsTheBestTeam { get; set; } = true;
    public TeamColor OurColor { get; set; }
    public bool EnableDebug { get; set; } = false;
}