namespace Tyr.Common.Config;

public enum TeamColor
{
    // the values should not be changed to match SSL protos
    Unknown = 0,
    Yellow = 1,
    Blue = 2
}

public static class TeamColorExtensions
{
    public static TeamColor Opposite(this TeamColor color)
    {
        return color switch
        {
            TeamColor.Yellow => TeamColor.Blue,
            TeamColor.Blue => TeamColor.Yellow,
            _ => TeamColor.Unknown
        };
    }
}