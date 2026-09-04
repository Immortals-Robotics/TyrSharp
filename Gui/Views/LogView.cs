using Cysharp.Text;
using Hexa.NET.ImGui;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;
using Tyr.Common.Debug.Db;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Logging;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Debug = Tyr.Common.Debug;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class LogView(IDebugDb debugDb) : IDisposable
{
    public static readonly string WindowTitle = $"{IconFonts.FontAwesome6.Terminal} Logs";
    [ConfigEntry(StorageType.User)] private static LogLevel LogLevel { get; set; } = LogLevel.Debug;
    [ConfigEntry(StorageType.User)] private static bool JournalMode { get; set; } = false;
    [ConfigEntry(StorageType.User)] internal static bool GroupJournalEntries { get; set; } = true;

    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
    private bool IsFiltering => _filter.IsActive();
    private int _filterTested;
    private int _filterPassed;

    internal sealed class PreparedData
    {
        public List<Entry> Entries { get; } = [];
        public List<JournalGroup> JournalEntries { get; } = [];

        public void Reset()
        {
            Entries.Clear();
            JournalEntries.Clear();
        }
    }

    internal void Prepare(PlaybackTime time, DebugFilterSnapshot filterSnapshot, PreparedData prepared)
    {
        prepared.Reset();
        if (JournalMode)
            PrepareJournal(time, filterSnapshot, prepared);
        else
            PreparePerFrame(time, filterSnapshot, prepared);
    }

    private readonly List<Entry> _queryScratch = [];

    private void PreparePerFrame(PlaybackTime time, DebugFilterSnapshot filterSnapshot, PreparedData prepared)
    {
        Func<Debug.Meta, bool> metaFilter = filterSnapshot.IsEnabled;

        foreach (var module in debugDb.QueryModules())
        {
            if (!filterSnapshot.IsEnabled(module))
                continue;

            var frame = time.GetVisibleFrame(debugDb, module);
            if (!frame.HasValue)
                continue;

            _queryScratch.Clear();
            debugDb.QueryInto(_queryScratch, module, frame.Value.Start, frame.Value.End, metaFilter: metaFilter);
            foreach (var log in _queryScratch)
            {
                if (log.Level < LogLevel)
                    continue;

                prepared.Entries.Add(log);
            }
        }
    }

    private readonly List<JournalGroup> _journalBuffer = [];
    private bool _prevGroupJournalEntries = true;

    private void PrepareJournal(PlaybackTime time, DebugFilterSnapshot filterSnapshot, PreparedData prepared)
    {
        if (GroupJournalEntries != _prevGroupJournalEntries)
        {
            debugDb.RebuildJournal(GroupJournalEntries);
            _prevGroupJournalEntries = GroupJournalEntries;
        }

        debugDb.FillJournal(_journalBuffer);
        var cutoffNs = time.Time.Nanoseconds;

        // Binary search: find exclusive upper bound (first group with FirstTime > cutoff)
        int lo = 0, hi = _journalBuffer.Count;
        while (lo < hi)
        {
            var mid = lo + (hi - lo) / 2;
            if (_journalBuffer[mid].FirstTime.Nanoseconds <= cutoffNs) lo = mid + 1;
            else hi = mid;
        }

        for (int i = 0; i < lo; i++)
        {
            var group = _journalBuffer[i];
            if (group.Entry.Level < LogLevel) continue;
            if (!filterSnapshot.IsEnabled(group.Entry.Meta)) continue;
            prepared.JournalEntries.Add(group);
        }
    }

    private readonly List<Entry> _filteredEntries = [];
    private readonly List<JournalGroup> _filteredJournalEntries = [];
    private bool _wasJournalMode;

    internal void Draw(PreparedData prepared, bool isLive)
    {
        if (ImGui.Begin(WindowTitle, ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            const ImGuiTableFlags flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable |
                                          ImGuiTableFlags.Reorderable |
                                          ImGuiTableFlags.HighlightHoveredColumn |
                                          ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH |
                                          ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY;

            if (ImGui.BeginTable("logs", 7, flags))
            {
                _filterTested = 0;
                _filterPassed = 0;

                ImGui.TableSetupScrollFreeze(0, 1);
                ImGui.TableSetupColumn("Icon",
                    ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide |
                    ImGuiTableColumnFlags.NoHeaderLabel | ImGuiTableColumnFlags.NoResize |
                    ImGuiTableColumnFlags.DefaultSort,
                    ImGui.GetFontSize());
                ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthStretch, 1.5f);
                ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide,
                    1f);
                ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.WidthStretch, 2f);
                ImGui.TableSetupColumn("Function", ImGuiTableColumnFlags.WidthStretch, 1f);
                ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch, 10f);

                ImGui.TableHeadersRow();

                var sortSpecs = ImGui.TableGetSortSpecs();

                ImGui.PushFont(FontRegistry.Instance.MonoFont, FontRegistry.Instance.MonoFont.LegacySize);

                DrawSearchAndFilterControls();

                if (JournalMode)
                {
                    var journalEntries = prepared.JournalEntries;

                    // On mode switch, force time-ascending sort.
                    if (!_wasJournalMode)
                    {
                        journalEntries.Sort((a, b) => a.FirstTime.CompareTo(b.FirstTime));
                        sortSpecs.SpecsDirty = false;
                    }
                    else if (!sortSpecs.IsNull && sortSpecs.SpecsCount > 0 && journalEntries.Count > 1)
                    {
                        var spec = sortSpecs.Specs[0];
                        journalEntries.Sort((a, b) =>
                        {
                            int result = spec.ColumnIndex switch
                            {
                                0 or 3 => a.Entry.Level.CompareTo(b.Entry.Level),
                                1 => a.FirstTime.CompareTo(b.FirstTime),
                                2 => string.Compare(a.Entry.Meta.Module, b.Entry.Meta.Module, StringComparison.Ordinal),
                                4 => string.Compare(a.Entry.Meta.File, b.Entry.Meta.File, StringComparison.Ordinal),
                                5 => string.Compare(a.Entry.Meta.Member, b.Entry.Meta.Member, StringComparison.Ordinal),
                                6 => string.Compare(a.Entry.Message, b.Entry.Message, StringComparison.Ordinal),
                                _ => 0
                            };
                            return spec.SortDirection == ImGuiSortDirection.Ascending ? result : -result;
                        });
                        sortSpecs.SpecsDirty = false;
                    }

                    _filteredJournalEntries.Clear();
                    foreach (var group in journalEntries)
                    {
                        _filterTested += 1;
                        if (!_filter.PassFilter(group.Entry.Message) &&
                            !_filter.PassFilter(group.Entry.Meta.Module))
                            continue;
                        _filterPassed += 1;
                        _filteredJournalEntries.Add(group);
                    }

                    ImGuiListClipperPtr clipper = ImGui.ImGuiListClipper();
                    clipper.Begin(_filteredJournalEntries.Count);
                    while (clipper.Step())
                    {
                        for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            ImGui.TableNextRow();
                            DrawJournalEntry(_filteredJournalEntries[i]);
                        }
                    }
                    clipper.End();
                }
                else
                {
                    var frameEntries = prepared.Entries;

                    if (!sortSpecs.IsNull && sortSpecs.SpecsCount > 0 && frameEntries.Count > 1)
                    {
                        var spec = sortSpecs.Specs[0];
                        frameEntries.Sort((a, b) =>
                        {
                            int result = spec.ColumnIndex switch
                            {
                                0 or 3 => a.Level.CompareTo(b.Level),
                                1 => a.Timestamp.CompareTo(b.Timestamp),
                                2 => string.Compare(a.Meta.Module, b.Meta.Module, StringComparison.Ordinal),
                                4 => string.Compare(a.Meta.File, b.Meta.File, StringComparison.Ordinal),
                                5 => string.Compare(a.Meta.Member, b.Meta.Member, StringComparison.Ordinal),
                                6 => string.Compare(a.Message, b.Message, StringComparison.Ordinal),
                                _ => 0
                            };
                            return spec.SortDirection == ImGuiSortDirection.Ascending ? result : -result;
                        });
                        sortSpecs.SpecsDirty = false;
                    }

                    _filteredEntries.Clear();
                    foreach (var log in frameEntries)
                    {
                        _filterTested += 1;
                        if (!_filter.PassFilter(log.Message) &&
                            !_filter.PassFilter(log.Meta.Module))
                            continue;
                        _filterPassed += 1;
                        _filteredEntries.Add(log);
                    }

                    ImGuiListClipperPtr clipper = ImGui.ImGuiListClipper();
                    clipper.Begin(_filteredEntries.Count);
                    while (clipper.Step())
                    {
                        for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
                        {
                            ImGui.TableNextRow();
                            DrawLogEntry(_filteredEntries[i]);
                        }
                    }
                    clipper.End();
                }

                _wasJournalMode = JournalMode;

                ImGui.PopFont();

                ImGui.EndTable();

                // Add status bar at bottom
                if (IsFiltering)
                {
                    ImGui.Separator();
                    ImGui.TextColored(Color.Zinc400, $"{_filterPassed} of {_filterTested} items matching");
                }
            }

            if (!isLive)
            {
                // Draw a red border to indicate that we're showing playback data, not live data.
                var min = ImGui.GetWindowPos();
                var max = min + ImGui.GetWindowSize();
                ImGui.GetWindowDrawList().AddRect(min, max, ImGui.ColorConvertFloat4ToU32(Color.Red), 0f, ImDrawFlags.None, 4f);
            }
        }

        ImGui.End();
    }

    private void DrawLogEntry(Entry log)
    {
        var color = log.Level switch
        {
            LogLevel.Trace => Color.Slate300,
            LogLevel.Debug => Color.Teal200,
            LogLevel.Information => Color.Sky200,
            LogLevel.Warning => Color.Yellow300,
            LogLevel.Error => Color.Red400,
            LogLevel.Critical => Color.Fuchsia400,
            _ => Color.White,
        };
        ImGui.PushStyleColor(ImGuiCol.Text, color.RGBA);

        ImGui.TableNextColumn();
        var icon = log.Level switch
        {
            LogLevel.Trace => IconFonts.FontAwesome6.UserSecret,
            LogLevel.Debug => IconFonts.FontAwesome6.Bug,
            LogLevel.Information => IconFonts.FontAwesome6.CircleExclamation,
            LogLevel.Warning => IconFonts.FontAwesome6.TriangleExclamation,
            LogLevel.Error => IconFonts.FontAwesome6.Radiation,
            LogLevel.Critical => IconFonts.FontAwesome6.SkullCrossbones,
            _ => IconFonts.FontAwesome6.Question,
        };

        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetFontSize() / 4f);
        ImGui.TextUnformatted(icon);

        ImGui.TableNextColumn();
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0:D2}:{1:D2}:{2:D2}.{3:D3}\0",
            (int)log.Timestamp.NormalizedHours,
            (int)log.Timestamp.NormalizedMinutes,
            (int)log.Timestamp.NormalizedSeconds,
            (int)log.Timestamp.NormalizedMilliseconds);
        ImGui.TextUnformatted(_stringBuilder.AsSpan());

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(log.Meta.Module);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Debug.EnumCache<LogLevel>.GetName(log.Level));

        ImGui.TableNextColumn();
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0}: {1}\0",
            Debug.PathCache.GetFileName(log.Meta.File), log.Meta.Line);
        ImGui.TextUnformatted(_stringBuilder.AsSpan());

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(log.Meta.Member);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(log.Message);

        ImGui.PopStyleColor();
    }

    private void DrawJournalEntry(JournalGroup group)
    {
        var log = group.Entry;
        var color = log.Level switch
        {
            LogLevel.Trace => Color.Slate300,
            LogLevel.Debug => Color.Teal200,
            LogLevel.Information => Color.Sky200,
            LogLevel.Warning => Color.Yellow300,
            LogLevel.Error => Color.Red400,
            LogLevel.Critical => Color.Fuchsia400,
            _ => Color.White,
        };
        ImGui.PushStyleColor(ImGuiCol.Text, color.RGBA);

        ImGui.TableNextColumn();
        var icon = log.Level switch
        {
            LogLevel.Trace => IconFonts.FontAwesome6.UserSecret,
            LogLevel.Debug => IconFonts.FontAwesome6.Bug,
            LogLevel.Information => IconFonts.FontAwesome6.CircleExclamation,
            LogLevel.Warning => IconFonts.FontAwesome6.TriangleExclamation,
            LogLevel.Error => IconFonts.FontAwesome6.Radiation,
            LogLevel.Critical => IconFonts.FontAwesome6.SkullCrossbones,
            _ => IconFonts.FontAwesome6.Question,
        };
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + ImGui.GetFontSize() / 4f);
        ImGui.TextUnformatted(icon);

        ImGui.TableNextColumn();
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0:D2}:{1:D2}:{2:D2}.{3:D3}\0",
            (int)log.Timestamp.NormalizedHours,
            (int)log.Timestamp.NormalizedMinutes,
            (int)log.Timestamp.NormalizedSeconds,
            (int)log.Timestamp.NormalizedMilliseconds);
        ImGui.TextUnformatted(_stringBuilder.AsSpan());

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(log.Meta.Module);

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(Debug.EnumCache<LogLevel>.GetName(log.Level));

        ImGui.TableNextColumn();
        _stringBuilder.Clear();
        _stringBuilder.AppendFormat("{0}: {1}\0",
            Debug.PathCache.GetFileName(log.Meta.File), log.Meta.Line);
        ImGui.TextUnformatted(_stringBuilder.AsSpan());

        ImGui.TableNextColumn();
        ImGui.TextUnformatted(log.Meta.Member);

        ImGui.TableNextColumn();
        if (group.Count > 1)
        {
            _stringBuilder.Clear();
            _stringBuilder.AppendFormat("[×{0}] {1}\0", group.Count, log.Message);
            ImGui.TextUnformatted(_stringBuilder.AsSpan());
        }
        else
        {
            ImGui.TextUnformatted(log.Message);
        }

        ImGui.PopStyleColor();
    }

    private void DrawSearchAndFilterControls()
    {
        // search bar
        ImGui.SameLine(0f, ImGui.GetFontSize() * 3f + 10f);

        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X * 0.7f - 24f);
        ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.TableGetHoveredColumn() == 6
            ? Color.Zinc600
            : Color.Zinc800);
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, Color.Zinc500);
        _filter.Draw("##search");

        ImGui.SameLine();

        if (IsFiltering)
        {
            if (ImGui.Button($"{IconFonts.FontAwesome6.Xmark}##clear"))
            {
                _filter.Clear();
            }
        }
        else
        {
            ImGui.TextDisabled($"{IconFonts.FontAwesome6.MagnifyingGlass}");
        }

        ImGui.SameLine(0f, 10f);
        ImGui.TextDisabled("|");

        // journal mode toggle
        ImGui.SameLine(0f, 10f);
        var journalActive = JournalMode;
        if (journalActive) ImGui.PushStyleColor(ImGuiCol.Button, Color.Sky700);
        if (ImGui.Button($"{IconFonts.FontAwesome6.BookOpen} Journal##journal"))
        {
            JournalMode = !JournalMode;
            Configurable.MarkChanged(StorageType.User);
        }
        if (journalActive) ImGui.PopStyleColor();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip("Journal mode: show all Information+ logs from session start to current time");

        // Group toggle — only meaningful in journal mode
        ImGui.SameLine(0f, 4f);
        if (!journalActive) ImGui.BeginDisabled();
        var groupActive = GroupJournalEntries;
        if (groupActive) ImGui.PushStyleColor(ImGuiCol.Button, Color.Sky700);
        if (ImGui.Button($"{IconFonts.FontAwesome6.LayerGroup}##group"))
        {
            GroupJournalEntries = !GroupJournalEntries;
            Configurable.MarkChanged(StorageType.User);
        }
        if (groupActive) ImGui.PopStyleColor();
        if (!journalActive) ImGui.EndDisabled();
        if (ImGui.IsItemHovered(ImGuiHoveredFlags.ForTooltip))
            ImGui.SetTooltip("Group repeated journal entries (rebuilds from disk when toggled)");

        ImGui.SameLine(0f, 10f);
        ImGui.TextDisabled("|");

        // log level
        ImGui.SameLine(0f, 10f);
        var names = Debug.EnumCache<LogLevel>.Names;
        var index = Debug.EnumCache<LogLevel>.GetIndex(LogLevel);
        ImGui.SetNextItemWidth(-1f);
        if (ImGui.Combo("Level", ref index, names, names.Length))
        {
            LogLevel = Debug.EnumCache<LogLevel>.GetByIndex(index);
            Configurable.MarkChanged(StorageType.User);
        }

        ImGui.PopStyleColor();
        ImGui.PopStyleColor();
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
    }
}
