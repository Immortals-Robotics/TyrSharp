namespace Tyr.Common.Data;

public enum TeamColor
{
    // the values should not be changed to match SSL protos
    Unknown = 0,
    Yellow = 1,
    Blue = 2
}

public static class TeamColorExtensions
{
    public static TeamColor Opposite(this TeamColor? color)
    {
        return color switch
        {
            TeamColor.Yellow => TeamColor.Blue,
            TeamColor.Blue => TeamColor.Yellow,
            _ => TeamColor.Unknown
        };
    }

    public static Debug.Drawing.Color ToColor(this TeamColor? color) => color?.ToColor() ?? Debug.Drawing.Color.Gray;

    public static Debug.Drawing.Color ToColor(this TeamColor color)
    {
        return color switch
        {
            TeamColor.Blue => Debug.Drawing.Color.Blue500,
            TeamColor.Yellow => Debug.Drawing.Color.Yellow300,
            _ => Debug.Drawing.Color.Gray
        };
    }
}