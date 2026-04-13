using System.Runtime.CompilerServices;
using MemoryPack;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;

namespace Tyr.Soccer.Navigation.Trajectory;

[MemoryPackable]
public partial record struct Trajectory2DDrawable : IEntry
{
    [MemoryPackIgnore] public Common.Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Common.Debug.Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public required Trajectory2D Trajectory { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }
}

[MemoryPackable]
public partial record struct Trajectory2DChainedDrawable : IEntry
{
    [MemoryPackIgnore] public Common.Time.Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Common.Debug.Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey => null;

    public required Trajectory2DChained Trajectory { get; init; }
    public Color Color { get; set; }
    public Options Options { get; set; }
}

internal static class TrajectoryDrawTypeRegistration
{
#pragma warning disable CA2255 // The 'ModuleInitializer' attribute should only be used in
    [ModuleInitializer]
    internal static void Initialize()
    {
        Common.Debug.DebugTypeRegistry.Register<Trajectory2DDrawable>();
        Common.Debug.DebugTypeRegistry.Register<Trajectory2DChainedDrawable>();
    }
#pragma warning restore CA2255
}
