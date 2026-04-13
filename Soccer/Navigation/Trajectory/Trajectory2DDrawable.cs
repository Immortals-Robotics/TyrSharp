using System.Runtime.CompilerServices;
using MemoryPack;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Navigation.Trajectory;

[MemoryPackable]
public partial record Trajectory2DDrawable : IDrawable
{
    public required Trajectory2D Trajectory { get; init; }
}

[MemoryPackable]
public partial record Trajectory2DChainedDrawable : IDrawable
{
    public required Trajectory2DChained Trajectory { get; init; }
}

public static class TrajectoryDrawableRegistration
{
    public const ushort Tag = DrawableUnionRegistry.ExternalTagMin;

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Initialize()
    {
        DrawableUnionRegistry.Register(Tag, typeof(Trajectory2DDrawable));
        DrawableUnionRegistry.Register(Tag + 1, typeof(Trajectory2DChainedDrawable));
    }
#pragma warning restore CA2255
}
