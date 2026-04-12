using System.Runtime.CompilerServices;
using MemoryPack;
using Tyr.Common.Debug;
using Tyr.Common.Time;

namespace Tyr.Tests.Gui.Db;

[MemoryPackable]
public partial record struct TestDebugEntry : IEntry
{
    [MemoryPackIgnore] public Timestamp Timestamp { get; set; }
    [MemoryPackIgnore] public Meta Meta { get; set; }
    [MemoryPackIgnore] public string? ShardKey { get; init; }

    public int Value { get; init; }
    public required string Label { get; init; }
}

internal static class TestDebugEntryRegistration
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        DebugTypeRegistry.Register<TestDebugEntry>();
    }
}
