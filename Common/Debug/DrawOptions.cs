namespace Tyr.Common.Debug;

public readonly record struct DrawOptions(
    bool Filled = false,
    float Thickness = 1f,
    float Duration = 0f)
{
}