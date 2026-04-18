using System.Diagnostics;
using System.Reflection;
using NativeFileDialogSharp;

namespace Tyr.Gui.Platform;

public readonly record struct SinglePathDialogResult(bool IsOk, string Path);

public readonly record struct MultiPathDialogResult(bool IsOk, IReadOnlyList<string> Paths);

public static class FileDialogService
{
    public static SinglePathDialogResult FileOpen(string filterList)
    {
        return UseNativeOrMacFallback(
            () =>
            {
                var result = Dialog.FileOpen(filterList);
                return new SinglePathDialogResult(result.IsOk, result.Path ?? string.Empty);
            },
            MacFileOpen);
    }

    public static MultiPathDialogResult FileOpenMultiple(string filterList)
    {
        return UseNativeOrMacFallback(
            () =>
            {
                var result = Dialog.FileOpenMultiple(filterList);
                return new MultiPathDialogResult(result.IsOk, result.Paths ?? []);
            },
            MacFileOpenMultiple);
    }

    public static SinglePathDialogResult FileSave(string filterList)
    {
        return UseNativeOrMacFallback(
            () =>
            {
                var result = Dialog.FileSave(filterList);
                return new SinglePathDialogResult(result.IsOk, result.Path ?? string.Empty);
            },
            MacFileSave);
    }

    public static SinglePathDialogResult FolderPicker()
    {
        return UseNativeOrMacFallback(
            () =>
            {
                var result = Dialog.FolderPicker();
                return new SinglePathDialogResult(result.IsOk, result.Path ?? string.Empty);
            },
            MacFolderPicker);
    }

    private static T UseNativeOrMacFallback<T>(Func<T> nativeDialog, Func<T> macFallback)
    {
        try
        {
            return nativeDialog();
        }
        catch (Exception ex) when (ShouldUseMacFallback(ex))
        {
            Log.ZLogWarning($"Native file dialog backend unavailable. Falling back to osascript. {ex.GetType().Name}: {ex.Message}");
            return macFallback();
        }
    }

    private static bool ShouldUseMacFallback(Exception ex)
    {
        if (!OperatingSystem.IsMacOS())
            return false;

        if (ex is DllNotFoundException or BadImageFormatException)
            return true;

        return ex switch
        {
            TypeInitializationException { InnerException: { } inner } => ShouldUseMacFallback(inner),
            TargetInvocationException { InnerException: { } inner } => ShouldUseMacFallback(inner),
            _ => false,
        };
    }

    private static SinglePathDialogResult MacFileOpen()
    {
        var path = RunAppleScript([
            "set selectedFile to choose file",
            "return POSIX path of selectedFile",
        ]);

        return string.IsNullOrWhiteSpace(path)
            ? new SinglePathDialogResult(false, string.Empty)
            : new SinglePathDialogResult(true, path);
    }

    private static MultiPathDialogResult MacFileOpenMultiple()
    {
        var output = RunAppleScript([
            "set selectedFiles to choose file with multiple selections allowed",
            "set oldDelimiters to AppleScript's text item delimiters",
            "set AppleScript's text item delimiters to linefeed",
            "set posixPaths to {}",
            "repeat with selectedFile in selectedFiles",
            "copy (POSIX path of selectedFile) to end of posixPaths",
            "end repeat",
            "set joinedPaths to posixPaths as text",
            "set AppleScript's text item delimiters to oldDelimiters",
            "return joinedPaths",
        ]);

        if (string.IsNullOrWhiteSpace(output))
            return new MultiPathDialogResult(false, []);

        var paths = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new MultiPathDialogResult(paths.Length > 0, paths);
    }

    private static SinglePathDialogResult MacFileSave()
    {
        var path = RunAppleScript([
            "set selectedFile to choose file name",
            "return POSIX path of selectedFile",
        ]);

        return string.IsNullOrWhiteSpace(path)
            ? new SinglePathDialogResult(false, string.Empty)
            : new SinglePathDialogResult(true, path);
    }

    private static SinglePathDialogResult MacFolderPicker()
    {
        var path = RunAppleScript([
            "set selectedFolder to choose folder",
            "return POSIX path of selectedFolder",
        ]);

        return string.IsNullOrWhiteSpace(path)
            ? new SinglePathDialogResult(false, string.Empty)
            : new SinglePathDialogResult(true, path);
    }

    private static string? RunAppleScript(IReadOnlyList<string> scriptLines)
    {
        using var process = new Process();
        process.StartInfo.FileName = "osascript";
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        foreach (var line in scriptLines)
        {
            process.StartInfo.ArgumentList.Add("-e");
            process.StartInfo.ArgumentList.Add(line);
        }

        process.Start();
        var output = process.StandardOutput.ReadToEnd().Trim();
        var error = process.StandardError.ReadToEnd().Trim();
        process.WaitForExit();

        if (process.ExitCode == 0)
            return output;

        if (error.Contains("User canceled", StringComparison.OrdinalIgnoreCase))
            return null;

        throw new InvalidOperationException($"osascript file dialog failed: {error}");
    }
}
