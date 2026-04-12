using System.Runtime.CompilerServices;
using MemoryPack;
using MemoryPack.Formatters;
using Tyr.Common.Debug.Drawing.Drawables;
using Path = Tyr.Common.Debug.Drawing.Drawables.Path;

namespace Tyr.Common.Debug.Drawing;

public static class DrawableUnionRegistry
{
    private static readonly Lock Sync = new();
    private static readonly Dictionary<ushort, Type> TypesByTag = [];
    private static readonly Dictionary<Type, ushort> TagsByType = [];

    public const ushort CommonTagMax = 999;
    public const ushort ExternalTagMin = 1000;

    public static void Register(ushort tag, Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (!typeof(IDrawable).IsAssignableFrom(type))
            throw new ArgumentException($"Drawable type {type.FullName} must implement {nameof(IDrawable)}.", nameof(type));

        lock (Sync)
        {
            if (TypesByTag.TryGetValue(tag, out var existingType) && existingType != type)
                throw new InvalidOperationException(
                    $"Drawable union tag {tag} is already registered for {existingType.FullName}.");

            if (TagsByType.TryGetValue(type, out var existingTag) && existingTag != tag)
                throw new InvalidOperationException(
                    $"Drawable type {type.FullName} is already registered with tag {existingTag}.");

            TypesByTag[tag] = type;
            TagsByType[type] = tag;

            var formatter = new DynamicUnionFormatter<IDrawable>(
                [.. TypesByTag.OrderBy(static pair => pair.Key).Select(static pair => (pair.Key, pair.Value))]);
            MemoryPackFormatterProvider.Register(formatter);
        }
    }
}

internal static class CommonDrawableUnionRegistration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        DrawableUnionRegistry.Register(0, typeof(Arc));
        DrawableUnionRegistry.Register(1, typeof(Arrow));
        DrawableUnionRegistry.Register(2, typeof(Circle));
        DrawableUnionRegistry.Register(3, typeof(Line));
        DrawableUnionRegistry.Register(4, typeof(LineSegment));
        DrawableUnionRegistry.Register(5, typeof(Path));
        DrawableUnionRegistry.Register(6, typeof(Point));
        DrawableUnionRegistry.Register(7, typeof(Rectangle));
        DrawableUnionRegistry.Register(8, typeof(Robot));
        DrawableUnionRegistry.Register(9, typeof(Text));
        DrawableUnionRegistry.Register(10, typeof(Triangle));
    }
}
