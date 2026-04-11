using System.Numerics;
using Hexa.NET.ImGui;
using NativeFileDialogSharp;
using Tyr.Common.Config;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class SessionsView(PlaybackSessionManager playbackSessions)
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.HardDrive} Sessions";
    [ConfigEntry(StorageType.User)] private static bool KeepWindowOpen { get; set; } = true;

    private readonly HashSet<string> _selectedMetadataPaths = [];
    private string _mergeLabelBuffer = string.Empty;
    private string _renameBuffer = string.Empty;
    private PlaybackSessionInfo? _renameSession;
    private bool _openRenamePopup;

    public bool IsOpen { get; private set; } = KeepWindowOpen;
    public int SourceRevision => playbackSessions.SourceRevision;
    public bool SourceIsLive => playbackSessions.UsingLive;
    public string CurrentSourceLabel => playbackSessions.CurrentSourceLabel;

    public void Open()
    {
        SetOpen(true);
    }

    public void SwitchToLive()
    {
        playbackSessions.SwitchToLive();
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        var isOpen = true;
        if (!ImGui.Begin(WindowTitle, ref isOpen))
        {
            SetOpen(isOpen);
            ImGui.End();
            return;
        }

        SetOpen(isOpen);

        DrawToolbar();
        ImGui.Separator();
        DrawSessionTable();

        ImGui.End();
    }

    private void DrawToolbar()
    {
        if (ImGui.Button($"{IconFonts.FontAwesome6.Rotate} Refresh"))
            playbackSessions.RefreshSessions();

        ImGui.SameLine();
        if (ImGui.Button($"{IconFonts.FontAwesome6.FileImport} Import"))
            ImportArchive();

        var selectedSessions = GetSelectedSessions();

        ImGui.SameLine();
        if (selectedSessions.Count >= 2)
        {
            ImGui.SetNextItemWidth(200f);
            ImGui.InputTextWithHint("##merge-label", "Shared label", ref _mergeLabelBuffer, 256);
            ImGui.SameLine();
            if (ImGui.Button($"{IconFonts.FontAwesome6.ObjectGroup} Merge Label"))
                MergeLabel(selectedSessions, _mergeLabelBuffer);
        }
    }

    private void DrawSessionTable()
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter | ImGuiTableFlags.BordersV |
                                      ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.Sortable;

        if (!ImGui.BeginTable("sessions", 6, flags, new Vector2(0f, -1f)))
            return;

        ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 42f);
        ImGui.TableSetupColumn("##open", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort, 64f);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2.2f);
        ImGui.TableSetupColumn("Created", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort, 160f);
        ImGui.TableSetupColumn("Range", ImGuiTableColumnFlags.WidthFixed, 130f);
        ImGui.TableSetupColumn("Machine", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableHeadersRow();

        foreach (var session in GetSortedSessions())
        {
            ImGui.PushID(session.MetadataPath);
            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            var selected = _selectedMetadataPaths.Contains(session.MetadataPath);
            if (ImGui.Checkbox($"##select-{session.MetadataPath}", ref selected))
            {
                SetSelected(session.MetadataPath, selected);
            }

            ImGui.TableNextColumn();
            var isCurrent = string.Equals(playbackSessions.CurrentSessionMetadataPath, session.MetadataPath, StringComparison.Ordinal);
            if (ImGui.Button(isCurrent
                    ? $"{IconFonts.FontAwesome6.CirclePlay}##open"
                    : $"{IconFonts.FontAwesome6.Play}##open"))
            {
                playbackSessions.OpenSession(session);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
                ImGui.SetTooltip("Open");

            ImGui.TableNextColumn();
            if (isCurrent)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, Color.Emerald400);
                ImGui.TextUnformatted($"{IconFonts.FontAwesome6.Radio} {session.DisplayName}");
                ImGui.PopStyleColor();
            }
            else
            {
                ImGui.TextUnformatted(session.DisplayName);
            }
            if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left))
                ToggleSelected(session.MetadataPath);
            if (ImGui.IsItemHovered() && ImGui.IsMouseDoubleClicked(ImGuiMouseButton.Left))
                playbackSessions.OpenSession(session);
            DrawRowContextMenu(session);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.Metadata.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.RangeLabel);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted(session.Metadata.MachineName);
            ImGui.PopID();
        }

        ImGui.EndTable();
        DrawRenamePopup();
    }

    private IEnumerable<PlaybackSessionInfo> GetSortedSessions()
    {
        var sessions = playbackSessions.Sessions;
        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.IsNull || sortSpecs.SpecsCount <= 0)
            return sessions;

        var sortSpec = sortSpecs.Specs[0];
        var descending = sortSpec.SortDirection == ImGuiSortDirection.Descending;
        sortSpecs.SpecsDirty = false;

        return sortSpec.ColumnIndex switch
        {
            2 => OrderBy(sessions, static session => session.DisplayName, descending, StringComparer.OrdinalIgnoreCase),
            3 => OrderBy(sessions, static session => session.Metadata.CreatedAtUtc, descending),
            4 => OrderBy(sessions, GetSessionRangeValue, descending),
            5 => OrderBy(sessions, static session => session.Metadata.MachineName, descending, StringComparer.OrdinalIgnoreCase),
            _ => sessions,
        };
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

    private void ToggleSelected(string metadataPath)
    {
        if (!_selectedMetadataPaths.Add(metadataPath))
            _selectedMetadataPaths.Remove(metadataPath);
    }

    private void SetSelected(string metadataPath, bool selected)
    {
        if (selected) _selectedMetadataPaths.Add(metadataPath);
        else _selectedMetadataPaths.Remove(metadataPath);
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

    private void DrawRowContextMenu(PlaybackSessionInfo session)
    {
        if (!ImGui.BeginPopupContextItem($"session-actions-{session.MetadataPath}"))
            return;

        if (ImGui.Selectable($"{IconFonts.FontAwesome6.Play} Open Session"))
            playbackSessions.OpenSession(session);

        if (ImGui.Selectable($"{IconFonts.FontAwesome6.FolderOpen} Reveal In Explorer"))
            playbackSessions.RevealInExplorer(session);

        if (ImGui.Selectable($"{IconFonts.FontAwesome6.FileExport} Export"))
            ExportSelectedSessions([session]);

        if (ImGui.Selectable($"{IconFonts.FontAwesome6.PenToSquare} Rename"))
        {
            _renameSession = session;
            _renameBuffer = session.Metadata.CaptureLabel ?? string.Empty;
            _openRenamePopup = true;
        }

        if (ImGui.Selectable($"{IconFonts.FontAwesome6.Trash} Delete"))
            DeleteSelectedSessions([session]);

        ImGui.EndPopup();
    }

    private void DrawRenamePopup()
    {
        if (_renameSession is null)
            return;

        if (_openRenamePopup)
        {
            ImGui.OpenPopup("Rename Session");
            _openRenamePopup = false;
        }

        var open = true;
        if (!ImGui.BeginPopupModal("Rename Session", ref open, ImGuiWindowFlags.AlwaysAutoResize))
        {
            if (!open)
            {
                _renameSession = null;
                _openRenamePopup = false;
            }
            return;
        }

        ImGui.TextUnformatted(_renameSession.DisplayName);
        ImGui.SetNextItemWidth(320f);
        ImGui.InputTextWithHint("##rename-modal", "Friendly name", ref _renameBuffer, 256);

        if (ImGui.Button("Save"))
        {
            Rename(_renameSession, _renameBuffer);
            _renameSession = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.SameLine();
        if (ImGui.Button("Cancel"))
        {
            _renameSession = null;
            ImGui.CloseCurrentPopup();
        }

        ImGui.EndPopup();
    }

    private static IEnumerable<PlaybackSessionInfo> OrderBy<TKey>(
        IEnumerable<PlaybackSessionInfo> sessions,
        Func<PlaybackSessionInfo, TKey> keySelector,
        bool descending,
        IComparer<TKey>? comparer = null)
    {
        if (descending)
            return comparer is null
                ? sessions.OrderByDescending(keySelector)
                : sessions.OrderByDescending(keySelector, comparer);

        return comparer is null
            ? sessions.OrderBy(keySelector)
            : sessions.OrderBy(keySelector, comparer);
    }

    private static double GetSessionRangeValue(PlaybackSessionInfo session)
    {
        if (session.Metadata.FirstFrameTimestamp.HasValue && session.Metadata.LastFrameTimestamp.HasValue)
            return (session.Metadata.LastFrameTimestamp.Value - session.Metadata.FirstFrameTimestamp.Value).Seconds;

        if (session.Metadata.ClosedAtUtc.HasValue)
            return (session.Metadata.ClosedAtUtc.Value - session.Metadata.CreatedAtUtc).TotalSeconds;

        return double.NegativeInfinity;
    }

    private static void MarkWindowStateChanged()
    {
        Configurable.MarkChanged(StorageType.User);
    }

    private void SetOpen(bool isOpen)
    {
        if (IsOpen == isOpen)
            return;

        IsOpen = isOpen;
        KeepWindowOpen = isOpen;
        MarkWindowStateChanged();
    }
}
