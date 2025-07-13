namespace Tyr.Soccer.Navigation.Obstacle;

[Flags]
public enum Physicality
{
    None = 0,
    Physical = 1 << 0,
    Virtual = 1 << 1,

    All = Physical | Virtual,
}