using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Config;

/// <summary>
/// Tomlet mappers for common value types, plus float handling that keeps config files free of
/// binary-conversion noise (a <c>1.2f</c> is written as <c>1.2</c>, not <c>1.2000000476837158</c>).
/// </summary>
public static class TomlMappers
{
    /// <summary>The shortest double that round-trips to the same float, so the file shows what the code says.</summary>
    public static double RoundTrip(float value) =>
        double.Parse(value.ToString("R", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    public static TomlDouble Double(float value) => new(RoundTrip(value));

    /// <summary>Like <see cref="TomletMain.ValueFrom(Type, object)"/> with float noise removed.</summary>
    public static TomlValue ValueFrom(Type type, object value)
    {
        if (value is float f) return Double(f);
        return TomletMain.ValueFrom(type, value)
               ?? throw new InvalidOperationException($"Value of type {type} serialized to null.");
    }

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in application code
    [ModuleInitializer]
    internal static void Register()
    {
        TomletMain.RegisterMapper<float>(
            f => Double(f),
            toml => (float)((toml as TomlDouble)?.Value ?? ((TomlLong)toml).Value));

        TomletMain.RegisterMapper(
            vec =>
            {
                var table = new TomlTable();
                table.PutValue(nameof(vec.X), Double(vec.X));
                table.PutValue(nameof(vec.Y), Double(vec.Y));
                return table;
            },
            toml =>
            {
                var table = (TomlTable)toml;
                return new Vector2(table.GetFloat(nameof(Vector2.X)), table.GetFloat(nameof(Vector2.Y)));
            });

        TomletMain.RegisterMapper(
            vec =>
            {
                var table = new TomlTable();
                table.PutValue(nameof(vec.X), Double(vec.X));
                table.PutValue(nameof(vec.Y), Double(vec.Y));
                table.PutValue(nameof(vec.Z), Double(vec.Z));
                return table;
            },
            toml =>
            {
                var table = (TomlTable)toml;
                return new Vector3(
                    table.GetFloat(nameof(Vector3.X)),
                    table.GetFloat(nameof(Vector3.Y)),
                    table.GetFloat(nameof(Vector3.Z)));
            });

        TomletMain.RegisterMapper(
            vec =>
            {
                var table = new TomlTable();
                table.PutValue(nameof(vec.X), Double(vec.X));
                table.PutValue(nameof(vec.Y), Double(vec.Y));
                table.PutValue(nameof(vec.Z), Double(vec.Z));
                table.PutValue(nameof(vec.W), Double(vec.W));
                return table;
            },
            toml =>
            {
                var table = (TomlTable)toml;
                return new Vector4(
                    table.GetFloat(nameof(Vector4.X)),
                    table.GetFloat(nameof(Vector4.Y)),
                    table.GetFloat(nameof(Vector4.Z)),
                    table.GetFloat(nameof(Vector4.W)));
            });
    }
#pragma warning restore CA2255
}
