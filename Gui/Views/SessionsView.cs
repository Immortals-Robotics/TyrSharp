using System.Numerics;
using Hexa.NET.ImGui;
using NativeFileDialogSharp;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;

namespace Tyr.Gui.Views;

public sealed class SessionsView(PlaybackSessionManager playbackSessions)
{
    private readonly HashSet<string> _selectedMetadataPaths = [];
    private string _renameBuffer = string.Empty;
    private string _mergeLabelBuffer = string.Empty;
    private string? _renameTargetMetadataPath;

    public bool IsOpen { get; set; }
    public int SourceRevision => playbackSessions.SourceRevision;
    public bool SourceIsLive => playbackSessions.UsingLive;

    public void Open()
    {
        IsOpen = true;
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        var isOpen = IsOpen;
        if (!ImGui.Begin($"{IconFonts.FontAwesome6.HardDrive} Sessions", ref isOpen))
        {
            IsOpen = isOpen;
            ImGui.End();
            return;
        }

        IsOpen = isOpen;

        DrawToolbar();
        ImGui.Separator();
        DrawSessionTable();

        ImGui.End();
    }

    private void DrawToolbar()
    {
        var wasUsingLive = playbackSessions.UsingLive;
        ImGui.TextDisabled($"Current Source: {playbackSessions.CurrentSourceLabel}");
        ImGui.SameLine();
        if (wasUsingLive)
            ImGui.BeginDisabled();
        if (ImGui.Button($"{IconFonts.FontAwesome6.SatelliteDish} Use Live"))
            playbackSessions.SwitchToLive();
        if (wasUsingLive)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.Rotate} Refresh"))
            playbackSessions.RefreshSessions();

        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.FileImport} Import"))
            ImportArchive();

        var selectedSessions = GetSelectedSessions();
        var singleSelection = selectedSessions.Count == 1 ? selectedSessions[0] : null;

        ImGui.SameLine();
        if (selectedSessions.Count == 0)
            ImGui.BeginDisabled();
        if (ImGui.Button($"{IconFonts.FontAwesome6.FileExport} Export"))
            ExportSelectedSessions(selectedSessions);
        if (selectedSessions.Count == 0)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (singleSelection is null)
            ImGui.BeginDisabled();
        if (ImGui.Button($"{IconFonts.FontAwesome6.FolderOpen} Reveal"))
            playbackSessions.RevealInExplorer(singleSelection!);
        if (singleSelection is null)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (singleSelection is null)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(220f);
        if (singleSelection is not null && _renameTargetMetadataPath != singleSelection.MetadataPath)
        {
            _renameTargetMetadataPath = singleSelection.MetadataPath;
            _renameBuffer = singleSelection.Metadata.CaptureLabel ?? string.Empty;
        }
        ImGui.InputTextWithHint("##rename", "Friendly name", ref _renameBuffer, 256);
        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.PenToSquare} Rename"))
            Rename(singleSelection!, _renameBuffer);
        if (singleSelection is null)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (selectedSessions.Count < 2)
            ImGui.BeginDisabled();
        ImGui.SetNextItemWidth(200f);
        ImGui.InputTextWithHint("##merge-label", "Shared label", ref _mergeLabelBuffer, 256);
        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.ObjectGroup} Merge Label"))
            MergeLabel(selectedSessions, _mergeLabelBuffer);
        if (selectedSessions.Count < 2)
            ImGui.EndDisabled();

        ImGui.SameLine();
        if (selectedSessions.Count == 0)
            ImGui.BeginDisabled();
        if (ImGui.Button($"{IconFonts.FontAwesome6.Trash} Delete"))
            DeleteSelectedSessions(selectedSessions);
        if (selectedSessions.Count == 0)
            ImGui.EndDisabled();
    }

    private void DrawSessionTable()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable;

        if (!ImGui.BeginTable("sessions", 7, flags, new Vector2(0f, -1f)))
            return;

        ImGui.TableSetupColumn("Sel", ImGuiTableColumnFlags.WidthFixed, 42f);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableSetupColumn("Created", ImGuiTableColumnFlags.WidthFixed, 160f);
        ImGui.TableSetupColumn("Range", ImGuiTableColumnFlags.WidthFixed, 130f);
        ImGui.TableSetupColumn("Source", ImGuiTableColumnFlags.WidthFixed, 90f);
        ImGui.TableSetupColumn("Machine", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableSetupColumn("Path", ImGuiTableColumnFlags.WidthStretch, 2f);
        ImGui.TableHeadersRow();

        foreach (var session in playbackSessions.Sessions)
        {
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var selected = _selectedMetadataPaths.Contains(session.MetadataPath);
            if (ImGui.Checkbox($"##select-{session.MetadataPath}", ref selected))
            {
                if (selected) _selectedMetadataPaths.Add(session.MetadataPath);
                else _selectedMetadataPaths.Remove(session.MetadataPath);
            }

            ImGui.TableNextColumn();
            var isCurrent = string.Equals(playbackSessions.CurrentSessionMetadataPath, session.MetadataPath, StringComparison.Ordinal);
            if (ImGui.Selectable(session.DisplayName, isCurrent, ImGuiSelectableFlags.SpanAllColumns))
            {
                playbackSessions.OpenSession(session);
            }

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.Metadata.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.RangeLabel);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(isCurrent ? "Opened" : playbackSessions.UsingLive ? "Stored" : "Stored");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.Metadata.MachineName);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.SessionDirectory);
        }

        ImGui.EndTable();
    }

    private List<PlaybackSessionInfo> GetSelectedSessions()
    {
        return playbackSessions.Sessions
            .Where(session => _selectedMetadataPaths.Contains(session.MetadataPath))
            .ToList();
    }

    private void ExportSelectedSessions(IReadOnlyList<PlaybackSessionInfo> sessions)
    {
        if (sessions.Count == 1)
        {
            var result = Dialog.FileSave("tyrlog;zip");
            if (!result.IsOk)
                return;

            playbackSessions.ExportSession(sessions[0], result.Path);
            return;
        }

        var folderResult = Dialog.FolderPicker();
        if (!folderResult.IsOk)
            return;

        foreach (var session in sessions)
        {
            var fileName = SanitizeFileName(session.DisplayName) + ".tyrlog";
            playbackSessions.ExportSession(session, Path.Combine(folderResult.Path, fileName));
        }
    }

    private void ImportArchive()
    {
        var result = Dialog.FileOpenMultiple("tyrlog;zip");
        if (!result.IsOk)
            return;

        foreach (var file in result.Paths)
            playbackSessions.ImportSessionArchive(file);

        playbackSessions.RefreshSessions();
    }

    private void Rename(PlaybackSessionInfo session, string label)
    {
        playbackSessions.RenameSession(session, NormalizeLabel(label));
        _renameTargetMetadataPath = session.MetadataPath;
        playbackSessions.RefreshSessions();
    }

    private void MergeLabel(IReadOnlyList<PlaybackSessionInfo> sessions, string label)
    {
        playbackSessions.AssignCaptureLabel(sessions, NormalizeLabel(label));
        playbackSessions.RefreshSessions();
    }

    private void DeleteSelectedSessions(IReadOnlyList<PlaybackSessionInfo> sessions)
    {
        playbackSessions.DeleteSessions(sessions);
        foreach (var session in sessions)
            _selectedMetadataPaths.Remove(session.MetadataPath);
        playbackSessions.RefreshSessions();
    }

    private static string? NormalizeLabel(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string SanitizeFileName(string value)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
        var builder = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
            builder.Append(invalidChars.Contains(ch) ? '_' : ch);
        return builder.ToString();
    }
}
