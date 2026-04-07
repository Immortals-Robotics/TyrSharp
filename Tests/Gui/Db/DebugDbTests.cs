using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;
using Entry = Tyr.Common.Debug.Logging.Entry;

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
