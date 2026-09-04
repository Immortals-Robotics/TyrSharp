using System.Numerics;
using Hexa.NET.ImGui;
using Tyr.Common.Config;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Tyr.Gui.Platform;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class SessionsView(PlaybackSessionManager playbackSessions)
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.HardDrive} Sessions";
    [ConfigEntry("Whether to restore the session window on startup.", StorageType.User)] private static partial bool KeepWindowOpen { get; set; } = true;
    [ConfigEntry("Automatically compress completed sessions into .tyrlog archives in the background.", StorageType.User)] public static partial bool AutoCompact { get; set; } = false;

    private readonly HashSet<string> _selectedMetadataPaths = [];
    private readonly Dictionary<string, string> _groupSelectedPaths = [];
    private readonly ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
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
        DrawSearchBar();
        ImGui.Separator();

        if (ImGui.Button($"{IconFonts.FontAwesome6.FileImport} Import"))
            ImportArchive();

        var selectedSessions = GetSelectedSessions();
        var canCompact = selectedSessions.Any(s => !s.IsCompacted);

        ImGui.SameLine();
        if (canCompact)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.FileZipper} Compact"))
            {
                playbackSessions.CompactSessions(selectedSessions.Where(s => !s.IsCompacted));
            }
        }

        var canCompactAny = playbackSessions.Sessions.Any(s => !s.IsCompacted);
        ImGui.SameLine();
        if (canCompactAny)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.BoxArchive} Compact All"))
            {
                playbackSessions.CompactSessions(playbackSessions.Sessions.Where(s => !s.IsCompacted));
            }
        }

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

        ImGui.TableSetupColumn("##sel", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoResize, 26f);
        ImGui.TableSetupColumn("##open", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoSort | ImGuiTableColumnFlags.NoResize, 32f);
        ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch, 2.2f);
        ImGui.TableSetupColumn("Created", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort, 160f);
        ImGui.TableSetupColumn("Duration", ImGuiTableColumnFlags.WidthFixed, 130f);
        ImGui.TableSetupColumn("Machine", ImGuiTableColumnFlags.WidthFixed, 120f);
        ImGui.TableHeadersRow();

        var sessions = playbackSessions.Sessions;
        if (_filter.IsActive())
        {
            sessions = sessions.Where(s => _filter.PassFilter(s.DisplayName) || _filter.PassFilter(s.Metadata.MachineName)).ToList();
        }

        var groups = sessions.GroupBy(s => s.Metadata.CaptureLabel).ToList();
        var processedGroups = new List<(string? Label, List<PlaybackSessionInfo> Sessions, PlaybackSessionInfo Active)>();

        foreach (var group in groups)
        {
            var label = group.Key;
            var groupSessions = group.OrderByDescending(s => s.Metadata.CreatedAtUtc).ToList();

            if (string.IsNullOrWhiteSpace(label))
            {
                foreach (var s in groupSessions)
                {
                    processedGroups.Add((null, [s], s));
                }
            }
            else
            {
                if (!_groupSelectedPaths.TryGetValue(label, out var activePath) || groupSessions.All(s => s.MetadataPath != activePath))
                {
                    activePath = groupSessions[0].MetadataPath;
                    _groupSelectedPaths[label] = activePath;
                }
                var activeSession = groupSessions.First(s => s.MetadataPath == activePath);
                processedGroups.Add((label, groupSessions, activeSession));
            }
        }

        // Apply sorting to the groups based on the Active session
        var sortedGroups = ApplyGroupSorting(processedGroups);

        foreach (var (label, groupSessions, active) in sortedGroups)
        {
            ImGui.PushID(label ?? active.MetadataPath);
            ImGui.TableNextRow();

            // Selection
            ImGui.TableNextColumn();
            var allSelected = groupSessions.All(s => _selectedMetadataPaths.Contains(s.MetadataPath));
            var anySelected = groupSessions.Any(s => _selectedMetadataPaths.Contains(s.MetadataPath));
            if (anySelected && !allSelected) ImGui.PushStyleColor(ImGuiCol.CheckMark, Color.Zinc400);

            var groupSel = allSelected;
            if (ImGui.Checkbox("##select", ref groupSel))
            {
                foreach (var s in groupSessions) SetSelected(s.MetadataPath, groupSel);
            }
            if (anySelected && !allSelected) ImGui.PopStyleColor();

            // Open
            ImGui.TableNextColumn();
            var isCurrent = string.Equals(playbackSessions.CurrentSessionMetadataPath, active.MetadataPath, StringComparison.Ordinal);
            if (ImGui.Button(isCurrent
                    ? $"{IconFonts.FontAwesome6.CirclePlay}##open"
                    : $"{IconFonts.FontAwesome6.Play}##open"))
            {
                playbackSessions.OpenSession(active);
            }
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
                ImGui.SetTooltip("Open selected session");

            // Name / Selector
            ImGui.TableNextColumn();
            if (active.IsCompacted)
            {
                ImGui.TextUnformatted(IconFonts.FontAwesome6.FileZipper);
                ImGui.SameLine();
            }

            if (label != null && groupSessions.Count > 1)
            {
                ImGui.SetNextItemWidth(-1f);
                if (ImGui.BeginCombo("##selector", $"{IconFonts.FontAwesome6.LayerGroup} {label} ({groupSessions.Count})"))
                {
                    foreach (var s in groupSessions)
                    {
                        var isSelected = s.MetadataPath == active.MetadataPath;
                        var sCurrent = string.Equals(playbackSessions.CurrentSessionMetadataPath, s.MetadataPath, StringComparison.Ordinal);
                        var sLabel = $"{s.Metadata.CreatedAtUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss} ({s.RangeLabel})";

                        if (sCurrent) ImGui.PushStyleColor(ImGuiCol.Text, Color.Emerald400);
                        if (ImGui.Selectable(sLabel, isSelected))
                        {
                            _groupSelectedPaths[label] = s.MetadataPath;
                        }
                        if (sCurrent) ImGui.PopStyleColor();

                        if (isSelected) ImGui.SetItemDefaultFocus();
                    }
                    ImGui.EndCombo();
                }
            }
            else
            {
                if (isCurrent)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, Color.Emerald400);
                    ImGui.TextUnformatted($"{IconFonts.FontAwesome6.Radio} {active.DisplayName}");
                    ImGui.PopStyleColor();
                }
                else
                {
                    ImGui.TextUnformatted(active.DisplayName);
                }
            }
            DrawRowContextMenu(active);

            // Created
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(active.Metadata.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"));

            // Range
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(active.RangeLabel);

            // Machine
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(active.Metadata.MachineName);

            ImGui.PopID();
        }

        ImGui.EndTable();
        DrawRenamePopup();
    }

    private List<(string? Label, List<PlaybackSessionInfo> Sessions, PlaybackSessionInfo Active)> ApplyGroupSorting(
        List<(string? Label, List<PlaybackSessionInfo> Sessions, PlaybackSessionInfo Active)> groups)
    {
        var sortSpecs = ImGui.TableGetSortSpecs();
        if (sortSpecs.IsNull || sortSpecs.SpecsCount <= 0)
            return groups;

        var sortSpec = sortSpecs.Specs[0];
        var descending = sortSpec.SortDirection == ImGuiSortDirection.Descending;
        sortSpecs.SpecsDirty = false;

        return sortSpec.ColumnIndex switch
        {
            2 => OrderByGroup(groups, g => g.Label ?? g.Active.DisplayName, descending, StringComparer.OrdinalIgnoreCase),
            3 => OrderByGroup(groups, g => g.Active.Metadata.CreatedAtUtc, descending),
            4 => OrderByGroup(groups, g => GetSessionRangeValue(g.Active), descending),
            5 => OrderByGroup(groups, g => g.Active.Metadata.MachineName, descending, StringComparer.OrdinalIgnoreCase),
            _ => groups,
        };
    }

    private static List<T> OrderByGroup<T, TKey>(
        List<T> groups,
        Func<T, TKey> keySelector,
        bool descending,
        IComparer<TKey>? comparer = null)
    {
        if (descending)
            return comparer is null
                ? groups.OrderByDescending(keySelector).ToList()
                : groups.OrderByDescending(keySelector, comparer).ToList();

        return comparer is null
            ? groups.OrderBy(keySelector).ToList()
            : groups.OrderBy(keySelector, comparer).ToList();
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
            var result = FileDialogService.FileSave("tyrlog;zip");
            if (!result.IsOk)
                return;

            playbackSessions.ExportSession(sessions[0], result.Path);
            return;
        }

        var folderResult = FileDialogService.FolderPicker();
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
        var result = FileDialogService.FileOpenMultiple("tyrlog;zip");
        if (!result.IsOk)
            return;

        foreach (var file in result.Paths)
            playbackSessions.ImportSessionArchive(file);
    }

    private void Rename(PlaybackSessionInfo session, string label)
    {
        playbackSessions.RenameSession(session, NormalizeLabel(label));
    }

    private void MergeLabel(IReadOnlyList<PlaybackSessionInfo> sessions, string label)
    {
        playbackSessions.AssignCaptureLabel(sessions, NormalizeLabel(label));
    }

    private void DeleteSelectedSessions(IReadOnlyList<PlaybackSessionInfo> sessions)
    {
        playbackSessions.DeleteSessions(sessions);
        foreach (var session in sessions)
            _selectedMetadataPaths.Remove(session.MetadataPath);
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

        if (!session.IsCompacted && ImGui.Selectable($"{IconFonts.FontAwesome6.FileZipper} Compact"))
        {
            playbackSessions.CompactSessions([session]);
        }

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

    private void DrawSearchBar()
    {
        ImGui.PushItemWidth(-32f);
        _filter.Draw("##search");
        ImGui.PopItemWidth();

        ImGui.SameLine();
        if (_filter.IsActive())
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##clear"))
                _filter.Clear();
        }
        else
        {
            ImGui.TextColored(Color.Zinc600, $"{IconFonts.FontAwesome6.MagnifyingGlass}");
        }
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

    private void SetOpen(bool isOpen)
    {
        if (IsOpen == isOpen)
            return;

        IsOpen = isOpen;
        KeepWindowOpen = isOpen;
    }
}
