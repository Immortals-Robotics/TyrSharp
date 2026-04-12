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

#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Initialize()
    {
        DrawableUnionRegistry.Register(Tag, typeof(TrajectoryDrawable));
    }
#pragma warning restore CA2255
}
