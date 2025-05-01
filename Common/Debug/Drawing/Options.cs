namespace Tyr.Common.Debug.Drawing;

public readonly record struct Options
{
    public bool IsFilled { get; init; }
    public float Thickness { get; init; }

    public static Options Filled => new() { IsFilled = true, Thickness = 0f };
    public static Options Outline(float thickness = 10f) => new() { IsFilled = false, Thickness = thickness };
}