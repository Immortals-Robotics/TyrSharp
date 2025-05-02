using System.Globalization;
using System.Numerics;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Debug.Drawing;

// ReSharper disable once InconsistentNaming
public readonly partial record struct Color(Vector4 RGBA)
{
    public float R => RGBA.X;
    public float G => RGBA.Y;
    public float B => RGBA.Z;
    public float A => RGBA.W;

    static Color()
    {
        TomletMain.RegisterMapper(
            color => new TomlString(color.ToHex()),
            toml => FromHex(toml.StringValue));
    }

    public Color(float r, float g, float b, float a = 1f) : this(new Vector4(r, g, b, a))
    {
    }

    public static Color FromHex(string hex)
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

    public string ToHex() => $"#{(byte)(R * 255f):X2}{(byte)(G * 255f):X2}{(byte)(B * 255f):X2}{(byte)(A * 255f):X2}";

    public Color WithAlpha(float alpha) => new Color(R, G, B, alpha);

    public static implicit operator Vector4(Color color) => color.RGBA;

    public static readonly Color White = FromHex("#ffffff");
    public static readonly Color Black = FromHex("#000000");

    public static readonly Color Invisible = FromHex("#ffffff").WithAlpha(0f);

    public override string ToString() => ToHex();
}