namespace Tyr.Common.Config;

public enum TeamColor
{
    Blue = 0,
    Yellow = 1
}

public static class TeamColorExtensions
{
    public static TeamColor Opposite(this TeamColor color)
    {
        return color == TeamColor.Blue ? TeamColor.Yellow : TeamColor.Blue;
    }
}