using System.Runtime.CompilerServices;
using MemoryPack;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Navigation.Trajectory;

[MemoryPackable]
public partial record TrajectoryDrawable : IDrawable
{
    public required Trajectory2D Trajectory { get; init; }
}

public static class TrajectoryDrawableRegistration
{
    public const ushort Tag = DrawableUnionRegistry.ExternalTagMin;

    [ModuleInitializer]
    internal static void Initialize()
    {
        DrawableUnionRegistry.Register(Tag, typeof(TrajectoryDrawable));
    }
}
