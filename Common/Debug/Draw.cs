using System.Runtime.CompilerServices;
using Tyr.Common.Shape;

namespace Tyr.Common.Debug;

public static partial class Debug
{
    public static void Draw<T>(T shape, Color color, bool filled = true, float thickness = 1f,
        [CallerMemberName] string? memberName = null,
        [CallerFilePath] string? filePath = null,
        [CallerLineNumber] int lineNumber = 0)
        where T : struct, IShape
    {
        DrawCommands<T>.Commands.Add(new DrawCommand<T>(shape, color, filled, thickness, memberName, filePath,
            lineNumber));
    }

    public static void ClearDrawCommands<T>() where T : struct, IShape
        => DrawCommands<T>.Commands.Clear();

    public static IReadOnlyList<DrawCommand<T>> GetDrawCommands<T>() where T : struct, IShape
        => DrawCommands<T>.Commands;
}