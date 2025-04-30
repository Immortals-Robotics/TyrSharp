using System.Globalization;
using System.Numerics;

namespace Tyr.Common.Debug.Drawing;

public readonly partial record struct Color
{
    // ReSharper disable once InconsistentNaming
    public Vector4 RGBA { get; }

    // ReSharper disable once InconsistentNaming
    public uint ABGR32 { get; }

    public float R => RGBA.X;
    public float G => RGBA.Y;
    public float B => RGBA.Z;
    public float A => RGBA.W;

    private Color(float r, float g, float b, float a = 1f)
    {
        RGBA = new Vector4(r, g, b, a);

        var ua = (uint)(a * 255.0f);
        var ub = (uint)(b * 255.0f);
        var ug = (uint)(g * 255.0f);
        var ur = (uint)(r * 255.0f);

        ABGR32 = (ua << 24) | (ub << 16) | (ug << 8) | ur;
    }

    private static Color FromHex(string hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
            throw new ArgumentException("Hex color string can't be empty", nameof(hex));

        var span = hex.StartsWith('#') ? hex.AsSpan(1) : hex.AsSpan(0);

        if (span.Length is not (6 or 8))
            throw new ArgumentException("Hex color string must be 6 or 8 characters long after the # character",
                nameof(hex));

        var r = byte.Parse(span[0..2], NumberStyles.HexNumber) / 255f;
        var g = byte.Parse(span[2..4], NumberStyles.HexNumber) / 255f;
        var b = byte.Parse(span[4..6], NumberStyles.HexNumber) / 255f;
        var a = span.Length == 8 ? byte.Parse(span[6..8], NumberStyles.HexNumber) / 255f : 1f;

        return new Color(r, g, b, a);
    }

    public Color WithAlpha(float alpha) => new Color(R, G, B, alpha);

    public override int GetHashCode() => (int)ABGR32;
    public bool Equals(Color other) => ABGR32 == other.ABGR32;

    public static implicit operator Vector4(Color color) => color.RGBA;

    public static readonly Color White = FromHex("#ffffff");
    public static readonly Color Black = FromHex("#000000");
    
    public static readonly Color Invisible = FromHex("#ffffff").WithAlpha(0f);
}