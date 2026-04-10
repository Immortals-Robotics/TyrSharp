using System.Diagnostics;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Plotting;
using Tyr.Common.Time;
using DrawingCommand = Tyr.Common.Debug.Drawing.Command;
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
    public void Append_OutOfOrderTimestamp_WritesDebugWarning()
    {
        var directory = CreateTempDirectory();

        try
        {
            using var db = new DebugDb(directory).RegisterType<Entry>();
            using var listener = new CollectingTraceListener();
            Trace.Listeners.Add(listener);

            try
            {
                db.Append(CreateEntry(100, "Vision", "first"));
                db.Append(CreateEntry(50, "Vision", "out-of-order"));
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }

#if DEBUG
            Assert.Contains("non-monotonic timestamp", listener.Messages, StringComparison.OrdinalIgnoreCase);
#else
            Assert.DoesNotContain("non-monotonic timestamp", listener.Messages, StringComparison.OrdinalIgnoreCase);
#endif
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
    public void Query_WithMaxCount_SamplesEvenlyAcrossMatchingEntries()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory).RegisterType<PlotCommand>())
            {
                for (int i = 0; i < 10; i++)
                {
                    db.Append(new PlotCommand
                    {
                        Value = PlotValue.From(i),
                        Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(Query_WithMaxCount_SamplesEvenlyAcrossMatchingEntries), line: 1),
                        ShardKey = "velocity",
                        Timestamp = Timestamp.FromNanoseconds(i),
                    });
                }
            }

            using var reopened = new DebugDb(directory).RegisterType<PlotCommand>();

            var commands = reopened.Query<PlotCommand>("Vision", Timestamp.Zero, Timestamp.FromNanoseconds(9), "velocity", 4).ToArray();

            Assert.Equal(4, commands.Length);
            Assert.Equal(0, commands[0].Timestamp.Nanoseconds);
            Assert.Equal(3, commands[1].Timestamp.Nanoseconds);
            Assert.Equal(6, commands[2].Timestamp.Nanoseconds);
            Assert.Equal(9, commands[3].Timestamp.Nanoseconds);
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

            Assert.Contains("Tyr.Common.Debug.Drawing.Command", typeDirectories);
            Assert.Contains("Tyr.Common.Debug.Plotting.Command", typeDirectories);
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
