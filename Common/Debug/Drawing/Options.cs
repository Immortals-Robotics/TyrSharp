namespace Tyr.Common.Debug.Drawing;

public readonly record struct Options(
    bool Filled = false,
    float Thickness = 1f,
    float Duration = 0f)
{
}