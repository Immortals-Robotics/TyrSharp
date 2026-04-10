using System.Diagnostics;
using System.Text;

namespace Tyr.Common.Debug.Db;

public static class DebugDbSessionPaths
{
    private const string ProductDirectoryName = "Tyr";
    private const string DebugDbDirectoryName = "debug-db";
    private const string SessionsDirectoryName = "sessions";

    public static string GetDefaultRootDirectory()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ProductDirectoryName,
            DebugDbDirectoryName);
    }

    public static DebugDbSessionDescriptor CreateSession(string? rootDirectory, string? captureLabel)
    {
        var resolvedRoot = string.IsNullOrWhiteSpace(rootDirectory)
            ? GetDefaultRootDirectory()
            : Path.GetFullPath(Environment.ExpandEnvironmentVariables(rootDirectory));

        var now = DateTimeOffset.UtcNow;
        var process = Process.GetCurrentProcess();
        var captureSlug = Slugify(captureLabel);
        var sessionId = $"{now:yyyyMMdd-HHmmss}-{process.ProcessName}-{process.Id}";
        var sessionDirectory = captureSlug.Length > 0
            ? Path.Combine(resolvedRoot, SessionsDirectoryName, captureSlug, sessionId)
            : Path.Combine(resolvedRoot, SessionsDirectoryName, sessionId);

        return new DebugDbSessionDescriptor(
            resolvedRoot,
            sessionDirectory,
            Path.Combine(sessionDirectory, "db"),
            Path.Combine(sessionDirectory, "session.json"),
            sessionId,
            string.IsNullOrWhiteSpace(captureLabel) ? null : captureLabel.Trim(),
            now);
    }

    private static string Slugify(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasSeparator = false;
        foreach (var ch in value.Trim())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(char.ToLowerInvariant(ch));
                previousWasSeparator = false;
                continue;
            }

            if (previousWasSeparator)
                continue;

            builder.Append('-');
            previousWasSeparator = true;
        }

        return builder.ToString().Trim('-');
    }
}

public sealed record DebugDbSessionDescriptor(
    string RootDirectory,
    string SessionDirectory,
    string DatabaseDirectory,
    string MetadataPath,
    string SessionId,
    string? CaptureLabel,
    DateTimeOffset CreatedAtUtc);
