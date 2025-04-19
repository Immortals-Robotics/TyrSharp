using ProtoBuf;

namespace Tyr.Common.Debug.Drawing;

public record struct Color(
    float R,
    float G,
    float B,
    float A = 1f)
{
    public Color Transparent => this with { A = A / 4f };

    public readonly uint U32 =>
        ((uint)(A * 255.0f) << 24) |
        ((uint)(B * 255.0f) << 16) |
        ((uint)(G * 255.0f) << 8) |
        ((uint)(R * 255.0f));

    public static Color Lerp(Color c1, Color c2, float t)
    {
        t = float.Clamp(t, 0f, 1f);

        return new Color(
            c1.R * (1f - t) + c2.R * t,
            c1.G * (1f - t) + c2.G * t,
            c1.B * (1f - t) + c2.B * t,
            c1.A * (1f - t) + c2.A * t);
    }

    // Common colors
    public static readonly Color White = new(1f, 1f, 1f);
    public static readonly Color Black = new(0f, 0f, 0f);
    public static readonly Color Red = new(0.902f, 0.1608f, 0.2157f);
    public static readonly Color Green = new(0.0f, 0.8941f, 0.1882f);
    public static readonly Color Blue = new(0.0f, 0.4745f, 0.9451f);
    public static readonly Color Yellow = new(0.9922f, 0.9765f, 0.0f);
    public static readonly Color Orange = new(1f, 0.6314f, 0f);
    public static readonly Color Magenta = new(1f, 0f, 1f);
}