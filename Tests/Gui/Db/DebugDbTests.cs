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
                    Id = "velocity",
                    Value = PlotValue.From(new Vector3(1, 2, 3)),
                    Title = "vel (mm/s)",
                    Meta = Meta.GetOrCreate("Vision", layer: "TestLayer", file: "DebugDbTests.cs", member: nameof(PlotCommands_RoundTripThroughDebugDb), line: 1),
                    Timestamp = Timestamp.FromNanoseconds(42),
                });
            }

            using var reopened = new DebugDb(directory);
            using var viewer = new DebugDbViewer(reopened).Register<PlotCommand>();

            var commands = reopened.QueryAll<PlotCommand>(Timestamp.Zero, Timestamp.MaxValue).ToArray();
            var command = Assert.Single(commands);

            Assert.Equal("velocity", command.Id);
            Assert.Equal("vel (mm/s)", command.Title);
            Assert.Equal(PlotValueKind.Vector3, command.Value.Kind);
            Assert.Equal(new Vector3(1, 2, 3), command.Value.Vector3Value);
            Assert.Equal("Vision", command.Meta.Module);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void RegisterType_UsesDistinctBucketsForSameNamedDebugCommands()
    {
        var directory = CreateTempDirectory();

        try
        {
            using (var db = new DebugDb(directory)
                       .RegisterType<DrawingCommand>()
                       .RegisterType<PlotCommand>())
            {
            }

            var recordFiles = Directory.GetFiles(directory, "*.records")
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .ToArray();

            Assert.Contains("Tyr.Common.Debug.Drawing.Command.records", recordFiles);
            Assert.Contains("Tyr.Common.Debug.Plotting.Command.records", recordFiles);
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
