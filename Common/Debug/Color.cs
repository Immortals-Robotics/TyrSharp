using ProtoBuf;

namespace Tyr.Common.Debug;

[ProtoContract]
public readonly struct Color(float r, float g, float b, float a = 1f)
{
    [ProtoMember(1)] public readonly float R = r;
    [ProtoMember(2)] public readonly float G = g;
    [ProtoMember(3)] public readonly float B = b;
    [ProtoMember(4)] public readonly float A = a;

    public Color Transparent => new(R, G, B, A / 4f);

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