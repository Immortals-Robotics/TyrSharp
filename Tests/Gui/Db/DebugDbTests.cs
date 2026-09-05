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
    public void DebugDbIngest_DrainsPublishedEntriesAndFramesIntoDb()
    {
        var directory = CreateTempDirectory();

        try
        {
            using var db = new DebugDb(directory);
            using var ingest = new DebugDbIngest(db);

            // The bus is process-global, so use a module name no other test publishes to.
            const string module = "IngestTestModule";
            var meta = Meta.GetOrCreate(module, layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(DebugDbIngest_DrainsPublishedEntriesAndFramesIntoDb), line: 1);

            DebugBus.Publish(new TestDebugEntry { Value = 1, Label = "first", Meta = meta, Timestamp = Timestamp.FromNanoseconds(10), ShardKey = "robot-1" });
            DebugBus.PublishFrame(new Frame { ModuleName = module, StartTimestamp = Timestamp.FromNanoseconds(20) });
            DebugBus.Publish(new TestDebugEntry { Value = 2, Label = "second", Meta = meta, Timestamp = Timestamp.FromNanoseconds(30), ShardKey = "robot-2" });
            DebugBus.Publish(CreateEntry(35, module, "ingested-log"));

            Assert.True(ingest.Pump());

            var entries = db.Query<TestDebugEntry>(module, Timestamp.Zero, Timestamp.MaxValue).ToArray();
            Assert.Equal(new[] { "first", "second" }, entries.Select(e => e.Label));
            Assert.Equal(new[] { 1, 2 }, entries.Select(e => e.Value));
            Assert.All(entries, e => Assert.Same(meta, e.Meta));
            Assert.Equal(new[] { "robot-1", "robot-2" }, db.QueryShardKeys<TestDebugEntry>(module));

            var log = Assert.Single(db.Query<Entry>(module, Timestamp.Zero, Timestamp.MaxValue));
            Assert.Equal("ingested-log", log.Message);

            var frame = Assert.Single(db.QueryFrames(module, Timestamp.Zero, Timestamp.MaxValue));
            Assert.Equal(20, frame.StartTimestamp.Nanoseconds);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void DebugDbIngest_PublishesFramesWhileAnEntryTypeIsStillBacklogged()
    {
        var directory = CreateTempDirectory();

        try
        {
            using var db = new DebugDb(directory);
            using var ingest = new DebugDbIngest(db);

            const string module = "IngestBacklogModule";
            const int budget = 10;
            const int entriesBeforeFrame = 100;
            const int entriesAfterFrame = 100;
            const long frameNs = entriesBeforeFrame / 2;

            var meta = Meta.GetOrCreate(module, layer: "TestLayer", file: "DebugDbTests.cs",
                member: nameof(DebugDbIngest_PublishesFramesWhileAnEntryTypeIsStillBacklogged), line: 1);

            // Flood one entry type far past the per-type budget, with a frame in the middle.
            for (var i = 1; i <= entriesBeforeFrame; i++)
                DebugBus.Publish(new TestDebugEntry { Value = i, Label = "flood", Meta = meta, Timestamp = Timestamp.FromNanoseconds(i) });

            DebugBus.PublishFrame(new Frame { ModuleName = module, StartTimestamp = Timestamp.FromNanoseconds(frameNs) });

            for (var i = 1; i <= entriesAfterFrame; i++)
            {
                DebugBus.Publish(new TestDebugEntry
                {
                    Value = entriesBeforeFrame + i,
                    Label = "flood",
                    Meta = meta,
                    Timestamp = Timestamp.FromNanoseconds(entriesBeforeFrame + i),
                });
            }

            // The frame must become visible while the entry channel is still backlogged, not
            // only once it has fully drained (which needs (100 + 100) / 10 = 20 pumps).
            var drainPumps = (entriesBeforeFrame + entriesAfterFrame) / budget;
            var pumps = 0;
            while (!db.QueryFrames(module, Timestamp.Zero, Timestamp.MaxValue).Any())
            {
                Assert.True(pumps < drainPumps, $"frame was still invisible after {pumps} pumps");
                ingest.Pump(budget);
                pumps++;
            }

            // Every entry up to the frame is in, and the rest is demonstrably still queued.
            var ingested = db.Query<TestDebugEntry>(module, Timestamp.Zero, Timestamp.MaxValue).Count();
            Assert.InRange(ingested, frameNs, entriesBeforeFrame + entriesAfterFrame - 1);

            var frame = Assert.Single(db.QueryFrames(module, Timestamp.Zero, Timestamp.MaxValue));
            Assert.Equal(frameNs, frame.StartTimestamp.Nanoseconds);

            // Draining the rest still lands every entry.
            for (var i = 0; i < drainPumps + 2; i++)
                ingest.Pump(budget);

            Assert.Equal(entriesBeforeFrame + entriesAfterFrame,
                db.Query<TestDebugEntry>(module, Timestamp.Zero, Timestamp.MaxValue).Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Bucket_GrowsPastInitialCapacityAndTruncatesOnDispose()
    {
        var directory = CreateTempDirectory();
        const int count = 20_000; // far more than the initial 64 KB records mapping holds

        try
        {
            var meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(Bucket_GrowsPastInitialCapacityAndTruncatesOnDispose), line: 1);
            using (var db = new DebugDb(directory).RegisterType<Entry>())
            {
                for (var i = 0; i < count; i++)
                {
                    db.Append(new Entry
                    {
                        Message = $"message-{i}",
                        Level = LogLevel.Debug,
                        Meta = meta,
                        Timestamp = Timestamp.FromNanoseconds(i),
                    });
                }

                var live = db.QueryAll<Entry>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
                Assert.Equal(count, live.Length);
            }

            // Files shrink to their used size once unmapped.
            var recordsFile = Assert.Single(Directory.GetFiles(directory, "*.records", SearchOption.AllDirectories));
            Assert.Equal(8 + count * 16L, new FileInfo(recordsFile).Length);

            using var reopened = new DebugDb(directory).RegisterType<Entry>();
            var entries = reopened.QueryAll<Entry>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            Assert.Equal(count, entries.Length);
            for (var i = 0; i < count; i++)
            {
                Assert.Equal(i, entries[i].Timestamp.Nanoseconds);
                Assert.Equal($"message-{i}", entries[i].Message);
            }

            // Appending after reopening a truncated file grows it again.
            reopened.Append(new Entry { Message = "after-reopen", Level = LogLevel.Debug, Meta = meta, Timestamp = Timestamp.FromNanoseconds(count) });
            Assert.Equal(count + 1, reopened.QueryAll<Entry>(Timestamp.Zero, Timestamp.MaxValue).Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void QueryInto_MetaFilterSkipsWholeShardsAndMergesTheRest()
    {
        var directory = CreateTempDirectory();

        try
        {
            using var db = new DebugDb(directory).RegisterType<Entry>();
            var keep = Meta.GetOrCreate("Vision", layer: "Keep", file: "DebugDbTests.cs", member: nameof(QueryInto_MetaFilterSkipsWholeShardsAndMergesTheRest), line: 1);
            var drop = Meta.GetOrCreate("Vision", layer: "Drop", file: "DebugDbTests.cs", member: nameof(QueryInto_MetaFilterSkipsWholeShardsAndMergesTheRest), line: 2);
            var other = Meta.GetOrCreate("Vision", layer: "Other", file: "DebugDbTests.cs", member: nameof(QueryInto_MetaFilterSkipsWholeShardsAndMergesTheRest), line: 3);

            // Three shards with interleaved timestamps.
            for (var i = 0; i < 30; i++)
            {
                var meta = (i % 3) switch { 0 => keep, 1 => drop, _ => other };
                db.Append(new Entry { Message = $"m{i}", Level = LogLevel.Debug, Meta = meta, Timestamp = Timestamp.FromNanoseconds(i) });
            }

            var results = new List<Entry>();
            var added = db.QueryInto(results, "Vision", Timestamp.Zero, Timestamp.MaxValue, metaFilter: m => !ReferenceEquals(m, drop));

            Assert.Equal(20, added);
            Assert.Equal(20, results.Count);
            Assert.DoesNotContain(results, e => ReferenceEquals(e.Meta, drop));
            for (var i = 1; i < results.Count; i++)
                Assert.True(results[i].Timestamp > results[i - 1].Timestamp, "merged output must stay timestamp ordered");

            // The destination list is appended to, never cleared.
            db.QueryInto(results, "Vision", Timestamp.Zero, Timestamp.FromNanoseconds(2));
            Assert.Equal(23, results.Count);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Queries_AreSafeWhileAnotherThreadAppendsAndInternsNewStrings()
    {
        var directory = CreateTempDirectory();

        try
        {
            using var db = new DebugDb(directory).RegisterType<PlotCommand>();
            var meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(Queries_AreSafeWhileAnotherThreadAppendsAndInternsNewStrings), line: 1);
            const int total = 20_000;

            // Writer: appends past several bucket growths and keeps interning new shard keys,
            // which is exactly what the reader-side string lookups race against.
            var writer = Task.Run(() =>
            {
                for (var i = 0; i < total; i++)
                {
                    db.Append(new PlotCommand
                    {
                        Value = PlotValue.From(i),
                        Meta = meta,
                        ShardKey = $"signal-{i % 64}",
                        Timestamp = Timestamp.FromNanoseconds(i),
                    });
                }
            });

            var results = new List<PlotCommand>();
            while (!writer.IsCompleted)
            {
                results.Clear();
                db.QueryInto(results, "Vision", Timestamp.Zero, Timestamp.MaxValue, maxCount: 100);
                db.QueryInto(results, "Vision", Timestamp.Zero, Timestamp.MaxValue, "signal-7");
                _ = db.TryGetShardMeta<PlotCommand>("Vision", "signal-63");
                _ = db.QueryShardKeys<PlotCommand>("Vision").Count();
                _ = db.QuerySourceLocations<PlotCommand>("Vision").Count();
            }

            await writer;

            results.Clear();
            Assert.Equal(total, db.QueryInto(results, "Vision", Timestamp.Zero, Timestamp.MaxValue));
            Assert.Equal(64, db.QueryShardKeys<PlotCommand>("Vision").Count());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
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
