using System.Numerics;
using System.Runtime.CompilerServices;
using Tomlet;
using Tomlet.Models;

namespace Tyr.Common.Config;

public static class VectorTomlMappers
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Register()
    {
        TomletMain.RegisterMapper(
            vec =>
            {
                var table = new TomlTable();
                table.PutValue(nameof(vec.X), new TomlDouble(vec.X));
                table.PutValue(nameof(vec.Y), new TomlDouble(vec.Y));
                return table;
            },
            toml =>
            {
                var table = (TomlTable)toml;
                var x = table.GetFloat(nameof(Vector2.X));
                var y = table.GetFloat(nameof(Vector2.Y));
                return new Vector2(x, y);
            });

        TomletMain.RegisterMapper(
            vec =>
            {
                var table = new TomlTable();
                table.PutValue(nameof(vec.X), new TomlDouble(vec.X));
                table.PutValue(nameof(vec.Y), new TomlDouble(vec.Y));
                table.PutValue(nameof(vec.Z), new TomlDouble(vec.Z));
                return table;
            },
            toml =>
            {
                var table = (TomlTable)toml;
                var x = table.GetFloat(nameof(Vector3.X));
                var y = table.GetFloat(nameof(Vector3.Y));
                var z = table.GetFloat(nameof(Vector3.Z));
                return new Vector3(x, y, z);
            });

        TomletMain.RegisterMapper(
            vec =>
            {
                var table = new TomlTable();
                table.PutValue(nameof(vec.X), new TomlDouble(vec.X));
                table.PutValue(nameof(vec.Y), new TomlDouble(vec.Y));
                table.PutValue(nameof(vec.Z), new TomlDouble(vec.Z));
                table.PutValue(nameof(vec.W), new TomlDouble(vec.W));
                return table;
            },
            toml =>
            {
                var table = (TomlTable)toml;
                var x = table.GetFloat(nameof(Vector4.X));
                var y = table.GetFloat(nameof(Vector4.Y));
                var z = table.GetFloat(nameof(Vector4.Z));
                var w = table.GetFloat(nameof(Vector4.W));
                return new Vector4(x, y, z, w);
            });
    }
#pragma warning restore CA2255
}