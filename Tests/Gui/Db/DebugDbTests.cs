using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;
using DrawingCommand = Tyr.Common.Debug.Drawing.Drawables.Point;
using Entry = Tyr.Common.Debug.Logging.Entry;
using PlotCommand = Tyr.Common.Debug.Plotting.Command;

namespace Tyr.Tests.Gui.Db;

public sealed class DebugDbTests
{
    [Fact]
    public void QueryFrames_RestoresFrameOnlyModulesAfterRestart()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory))
            {
                db.AppendFrame(new Frame
                {
                    ModuleName = "Vision",
                    StartTimestamp = Timestamp.FromNanoseconds(10),
                });
                db.AppendFrame(new Frame
                {
                    ModuleName = "Vision",
                    StartTimestamp = Timestamp.FromNanoseconds(20),
                });
            }

            using var reopened = new DebugDb(directory);

            var frames = reopened.QueryFrames("Vision", Timestamp.Zero, Timestamp.MaxValue).ToArray();
            Assert.Equal(2, frames.Length);
            Assert.All(frames, frame => Assert.Equal("Vision", frame.ModuleName));
            Assert.Equal(10, frames[0].StartTimestamp.Nanoseconds);
            Assert.Equal(20, frames[1].StartTimestamp.Nanoseconds);

            var frameAt = reopened.GetFrameAt("Vision", Timestamp.FromNanoseconds(15));
            Assert.NotNull(frameAt);
            Assert.Equal(10, frameAt.Value.Start.Nanoseconds);
            Assert.Equal(20, frameAt.Value.End.Nanoseconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ViewerRegister_LoadsPersistedBucketsForQueries()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory).RegisterType<Entry>())
            {
                db.Append(CreateEntry(123, "Vision", "viewer-register"));
            }

            using var reopened = new DebugDb(directory);
            Assert.Empty(reopened.QueryAll<Entry>(Timestamp.Zero, Timestamp.MaxValue));

            using var viewer = new DebugDbViewer(reopened).Register<Entry>();

            var entries = reopened.QueryAll<Entry>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            var entry = Assert.Single(entries);
            Assert.Equal("viewer-register", entry.Message);
            Assert.Equal("Vision", entry.Meta.Module);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void PlotCommands_RoundTripThroughDebugDb()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory).RegisterType<PlotCommand>())
            {
                db.Append(new PlotCommand
                {
                    Value = PlotValue.From(new Vector3(1, 2, 3)),
                    Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(PlotCommands_RoundTripThroughDebugDb), line: 1),
                    ShardKey = "velocity",
                    Timestamp = Timestamp.FromNanoseconds(42),
                });
            }

            using var reopened = new DebugDb(directory);
            using var viewer = new DebugDbViewer(reopened).Register<PlotCommand>();

            var commands = reopened.QueryAll<PlotCommand>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            var command = Assert.Single(commands);

            Assert.Equal(PlotValueKind.Vector3, command.Value.Kind);
            Assert.Equal(new Vector3(1, 2, 3), command.Value.Vector3Value);
            Assert.Equal("Vision", command.Meta.Module);

            var velocityCommands = reopened.Query<PlotCommand>("Vision", Timestamp.Zero, Timestamp.MaxValue, "velocity").ToArray();
            Assert.Single(velocityCommands);

            var missingCommands = reopened.Query<PlotCommand>("Vision", Timestamp.Zero, Timestamp.MaxValue, "missing").ToArray();
            Assert.Empty(missingCommands);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Query_WithMaxCount_SamplesWithTimeBucketsAcrossMatchingEntries()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory).RegisterType<PlotCommand>())
            {
                // 100 entries spanning 1 second (10ms apart)
                for (int i = 0; i < 100; i++)
                {
                    db.Append(new PlotCommand
                    {
                        Value = PlotValue.From(i),
                        Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(Query_WithMaxCount_SamplesWithTimeBucketsAcrossMatchingEntries), line: 1),
                        ShardKey = "velocity",
                        Timestamp = Timestamp.FromNanoseconds(i * 10_000_000L), // 10ms steps → 1s total
                    });
                }
            }

            using var reopened = new DebugDb(directory).RegisterType<PlotCommand>();

            var t0 = Timestamp.Zero;
            var t1 = Timestamp.FromNanoseconds(99 * 10_000_000L);
            const int maxCount = 10;
            var commands = reopened.Query<PlotCommand>("Vision", t0, t1, "velocity", maxCount).ToArray();

            // Time-bucket sampling: bucketSize = rangeNs / maxCount.
            // Count is approximate (≈ maxCount); exact count may vary by ±1.
            Assert.InRange(commands.Length, maxCount - 1, maxCount + 1);

            // First sample is at or near the start, last at or near the end.
            Assert.Equal(0, commands[0].Timestamp.Nanoseconds);
            Assert.Equal(99 * 10_000_000L, commands[^1].Timestamp.Nanoseconds);

            // Samples are monotonically increasing and span the full range.
            for (int i = 1; i < commands.Length; i++)
                Assert.True(commands[i].Timestamp > commands[i - 1].Timestamp);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RegisterType_UsesDistinctTypeDirectoriesForSameNamedDebugCommands()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory)
                       .RegisterType<DrawingCommand>()
                       .RegisterType<PlotCommand>())
            {
            }

            var typeDirectories = Directory.GetDirectories(directory)
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .ToArray();

            Assert.Contains("Tyr.Common.Debug.Drawing.Drawables.Point", typeDirectories);
            Assert.Contains("Tyr.Common.Debug.Plotting.Command", typeDirectories);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DebugTypeRegistry_IncludesCustomTestEntryOutsideCommon()
    {
        var types = DebugTypeRegistry.GetRegisteredTypes();
        Assert.Contains(typeof(TestDebugEntry), types);
    }

    [Fact]
    public void DebugBus_PublishesCustomEntryThroughGenericTransport()
    {
        using var subscriber = DebugBus.Subscribe<TestDebugEntry>(Tyr.Common.Dataflow.Mode.All);

        var entry = new TestDebugEntry
        {
            Value = 7,
            Label = "custom",
            Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(DebugBus_PublishesCustomEntryThroughGenericTransport), line: 1),
            Timestamp = Timestamp.FromNanoseconds(77),
            ShardKey = "robot-1",
        };

        DebugBus.Publish(entry);

        Assert.True(subscriber.Reader.TryRead(out var published));
        Assert.Equal(entry.Value, published.Value);
        Assert.Equal(entry.Label, published.Label);
        Assert.Equal(entry.Meta.Module, published.Meta.Module);
        Assert.Equal(entry.Timestamp, published.Timestamp);
        Assert.Equal(entry.ShardKey, published.ShardKey);
    }

    [Fact]
    public void DebugBus_PublishesFramesAndEntriesThroughOrderedDumpTransport()
    {
        using var subscriber = DebugBus.SubscribeDumpEntries(Tyr.Common.Dataflow.Mode.All);

        var firstEntry = new TestDebugEntry
        {
            Value = 1,
            Label = "first",
            Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(DebugBus_PublishesFramesAndEntriesThroughOrderedDumpTransport), line: 1),
            Timestamp = Timestamp.FromNanoseconds(10),
            ShardKey = "robot-1",
        };
        var frame = new Frame
        {
            ModuleName = "Vision",
            StartTimestamp = Timestamp.FromNanoseconds(20),
        };
        var secondEntry = new TestDebugEntry
        {
            Value = 2,
            Label = "second",
            Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(DebugBus_PublishesFramesAndEntriesThroughOrderedDumpTransport), line: 1),
            Timestamp = Timestamp.FromNanoseconds(30),
            ShardKey = "robot-2",
        };

        DebugBus.Publish(firstEntry);
        DebugBus.PublishFrame(frame);
        DebugBus.Publish(secondEntry);

        // The dump channel is a global static bus, so entries published by
        // tests running in parallel can interleave with ours. Skip foreign
        // traffic and assert only the relative order of our own publishes.
        var observed = new List<string>();
        while (subscriber.Reader.TryRead(out var published))
        {
            if (published.TryGetEntry<TestDebugEntry>(out var typed))
            {
                if (typed.Label == firstEntry.Label)
                {
                    Assert.Equal(firstEntry.Value, typed.Value);
                    observed.Add("first");
                }
                else if (typed.Label == secondEntry.Label)
                {
                    Assert.Equal(secondEntry.Value, typed.Value);
                    observed.Add("second");
                }
            }
            else if (published.TryGetFrame(out var typedFrame) &&
                     typedFrame.ModuleName == frame.ModuleName &&
                     typedFrame.StartTimestamp == frame.StartTimestamp)
            {
                observed.Add("frame");
            }
        }

        Assert.Equal(new[] { "first", "frame", "second" }, observed);
    }

    [Fact]
    public void RegisterKnownTypes_RoundTripsCustomEntryOutsideCommon()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory).RegisterKnownTypes())
            {
                db.Append(new TestDebugEntry
                {
                    Value = 42,
                    Label = "outside-common",
                    Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(RegisterKnownTypes_RoundTripsCustomEntryOutsideCommon), line: 1),
                    Timestamp = Timestamp.FromNanoseconds(123),
                    ShardKey = "robot-2",
                });
            }

            using var reopened = new DebugDb(directory).RegisterKnownTypes();
            var entries = reopened.QueryAll<TestDebugEntry>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            var entry = Assert.Single(entries);
            Assert.Equal(42, entry.Value);
            Assert.Equal("outside-common", entry.Label);
            Assert.Equal("Vision", entry.Meta.Module);

            var filtered = reopened.Query<TestDebugEntry>("Vision", Timestamp.Zero, Timestamp.MaxValue, "robot-2").ToArray();
            Assert.Single(filtered);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void ViewerRegisterAllRegisteredTypes_LoadsCustomEntryType()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory).RegisterKnownTypes())
            {
                db.Append(new TestDebugEntry
                {
                    Value = 9,
                    Label = "viewer-custom",
                    Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(ViewerRegisterAllRegisteredTypes_LoadsCustomEntryType), line: 1),
                    Timestamp = Timestamp.FromNanoseconds(999),
                    ShardKey = "robot-3",
                });
            }

            using var reopened = new DebugDb(directory);
            Assert.Empty(reopened.QueryAll<TestDebugEntry>(Timestamp.Zero, Timestamp.MaxValue));

            using var viewer = new DebugDbViewer(reopened).RegisterAllRegisteredTypes();
            var entries = reopened.QueryAll<TestDebugEntry>(Timestamp.Zero, Timestamp.MaxValue).ToArray();

            var entry = Assert.Single(entries);
            Assert.Equal("viewer-custom", entry.Label);
            Assert.Equal(9, entry.Value);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static Entry CreateEntry(long timestampNs, string module, string message)
    {
        return new Entry
        {
            Message = message,
            Level = LogLevel.Information,
            Meta = Meta.GetOrCreate(module, layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(CreateEntry), line: 1),
            Timestamp = Timestamp.FromNanoseconds(timestampNs),
        };
    }

    private static string CreateTempDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "TyrSharp.DebugDbTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    private sealed class CollectingTraceListener : TraceListener
    {
        private readonly List<string> _messages = [];

        public string Messages => string.Join(Environment.NewLine, _messages);

        public override void Write(string? message)
        {
            if (message is not null)
                _messages.Add(message);
        }

        public override void WriteLine(string? message)
        {
            if (message is not null)
                _messages.Add(message);
        }
    }

}
