using Tyr.Common.Debug;
using Tyr.Common.Debug.Db;
using Tyr.Common.Time;

namespace Tyr.Tests.Gui.Db;

public sealed class DebugDbSessionPathsTests
{
    [Fact]
    public void CreateSession_UsesCaptureSlugInPath()
    {
        var root = Path.Combine(Path.GetTempPath(), "TyrSharp.DebugDbSessionPathsTests", Guid.NewGuid().ToString("N"));

        var session = DebugDbSessionPaths.CreateSession(root, "Thursday Afternoon Motion Test");

        Assert.Equal(root, session.RootDirectory);
        Assert.Contains(Path.Combine("sessions", "thursday-afternoon-motion-test"), session.SessionDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith(Path.Combine("db"), session.DatabaseDirectory, StringComparison.OrdinalIgnoreCase);
        Assert.EndsWith("session.json", session.MetadataPath, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Metadata_SaveWritesFrameRange()
    {
        var root = Path.Combine(Path.GetTempPath(), "TyrSharp.DebugDbSessionMetadataTests", Guid.NewGuid().ToString("N"));
        var session = DebugDbSessionPaths.CreateSession(root, "semi-final against Tigers");
        var metadata = DebugDbSessionMetadata.Create(session);

        try
        {
            metadata.UpdateFrameRange(new Frame
            {
                ModuleName = "Vision",
                StartTimestamp = Timestamp.FromNanoseconds(10),
            });
            metadata.UpdateFrameRange(new Frame
            {
                ModuleName = "Vision",
                StartTimestamp = Timestamp.FromNanoseconds(25),
            });
            metadata.Save(session.MetadataPath);

            var json = File.ReadAllText(session.MetadataPath);
            Assert.Contains("\"captureLabel\": \"semi-final against Tigers\"", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"firstFrameTimestamp\": {", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"nanoseconds\": 10", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"lastFrameTimestamp\": {", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("\"nanoseconds\": 25", json, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
