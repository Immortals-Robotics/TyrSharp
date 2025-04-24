namespace Tyr.Common.Debug.Drawing;

public readonly record struct Color(
    float R,
    float G,
    float B,
    float A = 1f)
{
    public Color Transparent() => this with { A = A / 4f };

    // ReSharper disable once InconsistentNaming
    public uint ToABGR32()
    {
        var a = (uint)(A * 255.0f);
        var b = (uint)(B * 255.0f);
        var g = (uint)(G * 255.0f);
        var r = (uint)(R * 255.0f);

        return (a << 24) | (b << 16) | (g << 8) | r;
    }

    public static Color Lerp(Color from, Color to, float t)
    {
        t = float.Clamp(t, 0f, 1f);

        return new Color(
            from.R * (1f - t) + to.R * t,
            from.G * (1f - t) + to.G * t,
            from.B * (1f - t) + to.B * t,
            from.A * (1f - t) + to.A * t);
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

    public static Color Random()
    {
        ReadOnlySpan<Color> colors = [White, Black, Red, Green, Blue, Yellow, Orange, Magenta];
        return Rand.Get(colors);
    }

    public override string ToString() => $"(r: {R:0.##}, g:{G:0.##}, b:{B:0.##}, a:{A:0.##})";
}