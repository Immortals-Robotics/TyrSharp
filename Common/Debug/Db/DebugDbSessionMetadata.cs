using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using Tyr.Common.Debug;
using Tyr.Common.Time;

namespace Tyr.Common.Debug.Db;

public sealed class DebugDbSessionMetadata
{
    public const int CurrentSchemaVersion = 1;

    public required int SchemaVersion { get; init; }
    public required string SessionId { get; init; }
    public string? CaptureLabel { get; init; }
    public required string SessionDirectory { get; init; }
    public required string DatabaseDirectory { get; init; }
    public required DateTimeOffset CreatedAtUtc { get; init; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
    public Timestamp? FirstFrameTimestamp { get; set; }
    public Timestamp? LastFrameTimestamp { get; set; }
    public required string MachineName { get; init; }
    public required string UserName { get; init; }
    public required string ProcessName { get; init; }
    public required int ProcessId { get; init; }
    public required string? EntryAssemblyVersion { get; init; }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
    };

    public static DebugDbSessionMetadata Create(DebugDbSessionDescriptor session)
    {
        var process = Process.GetCurrentProcess();
        var entryAssembly = Assembly.GetEntryAssembly();
        return new DebugDbSessionMetadata
        {
            SchemaVersion = CurrentSchemaVersion,
            SessionId = session.SessionId,
            CaptureLabel = session.CaptureLabel,
            SessionDirectory = session.SessionDirectory,
            DatabaseDirectory = session.DatabaseDirectory,
            CreatedAtUtc = session.CreatedAtUtc,
            MachineName = Environment.MachineName,
            UserName = Environment.UserName,
            ProcessName = process.ProcessName,
            ProcessId = process.Id,
            EntryAssemblyVersion = entryAssembly?.GetName().Version?.ToString(),
        };
    }

    public void UpdateFrameRange(Frame frame)
    {
        if (!FirstFrameTimestamp.HasValue || frame.StartTimestamp < FirstFrameTimestamp.Value)
            FirstFrameTimestamp = frame.StartTimestamp;

        if (!LastFrameTimestamp.HasValue || frame.StartTimestamp > LastFrameTimestamp.Value)
            LastFrameTimestamp = frame.StartTimestamp;
    }

    public void Save(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
    }

    public static DebugDbSessionMetadata Load(string path)
    {
        var metadata = JsonSerializer.Deserialize<DebugDbSessionMetadata>(File.ReadAllText(path), JsonOptions);
        return metadata ?? throw new InvalidOperationException($"Failed to deserialize debug DB session metadata at {path}.");
    }

    public string ResolveSessionDirectory(string metadataPath)
    {
        return Path.GetDirectoryName(metadataPath) ?? SessionDirectory;
    }

    public string ResolveDatabaseDirectory(string metadataPath)
    {
        var sessionDirectory = ResolveSessionDirectory(metadataPath);
        var relativeDbDirectory = Path.Combine(sessionDirectory, "db");
        return Directory.Exists(relativeDbDirectory) ? relativeDbDirectory : DatabaseDirectory;
    }
}
