using Cysharp.Text;
using Hexa.NET.ImGui;
using Microsoft.Extensions.Logging;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Gui.Backend;
using Tyr.Gui.Data;
using Debug = Tyr.Common.Debug;

namespace Tyr.Gui.Views;

[Configurable]
public sealed partial class LogView(DebugFramer debugFramer, DebugFilter filter) : IDisposable
{
    [ConfigEntry] private static LogLevel LogLevel { get; set; } = LogLevel.Debug;

    private Utf8ValueStringBuilder _stringBuilder = ZString.CreateUtf8StringBuilder();

    private ImGuiTextFilterPtr _filter = ImGui.ImGuiTextFilter();
    private bool IsFiltering => _filter.IsActive();

    public void Draw(PlaybackTime time)
    {
        if (ImGui.Begin($"{IconFonts.FontAwesome6.Terminal} Logs", ImGuiWindowFlags.AlwaysVerticalScrollbar))
        {
            DrawHeader();
            DrawLogTable(time);
        }

        ImGui.End();
    }

    private void DrawHeader()
    {
        var names = Debug.EnumCache<LogLevel>.Names;
        var index = Debug.EnumCache<LogLevel>.GetIndex(LogLevel);
        ImGui.SetNextItemWidth(MathF.Min(200f, ImGui.GetContentRegionAvail().X / 3f));
        if (ImGui.Combo("Level", ref index, names, names.Length))
        {
            LogLevel = Debug.EnumCache<LogLevel>.GetByIndex(index);
            Configurable.OnChanged();
        }
    }

    private void DrawLogTable(PlaybackTime time)
    {
        const ImGuiTableFlags flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.Hideable |
                                      ImGuiTableFlags.Reorderable |
                                      ImGuiTableFlags.HighlightHoveredColumn |
                                      ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH;

        if (ImGui.BeginTable("logs", 7, flags))
        {
            ImGui.TableSetupColumn("Icon",
                ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.NoHide |
                ImGuiTableColumnFlags.NoHeaderLabel | ImGuiTableColumnFlags.NoResize,
                ImGui.GetFontSize());
            ImGui.TableSetupColumn("Time", ImGuiTableColumnFlags.WidthStretch, 1.5f);
            ImGui.TableSetupColumn("Module", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Level", ImGuiTableColumnFlags.WidthStretch | ImGuiTableColumnFlags.DefaultHide,
                1f);
            ImGui.TableSetupColumn("File", ImGuiTableColumnFlags.WidthStretch, 2f);
            ImGui.TableSetupColumn("Function", ImGuiTableColumnFlags.WidthStretch, 1f);
            ImGui.TableSetupColumn("Text", ImGuiTableColumnFlags.WidthStretch, 10f);

            ImGui.TableHeadersRow();

            ImGui.SameLine();
            var spacing = MathF.Max(50f, ImGui.GetContentRegionAvail().X - 300f);
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spacing);
            ImGui.SetNextItemWidth(-24f);
            ImGui.PushStyleColor(ImGuiCol.FrameBg, ImGui.TableGetHoveredColumn() == 6
                ? Color.Zinc600
                : Color.Zinc800);
            _filter.Draw("##search");
            ImGui.PopStyleColor();
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

            ImGui.PushFont(FontRegistry.Instance.MonoFont);

            foreach (var (module, framer) in debugFramer.Modules)
            {
                if (!filter.IsEnabled(module)) continue;

                var frame = time.Live ? framer.LatestFrame : framer.GetFrame(time.Time);
                if (frame == null) continue;

                foreach (var log in frame.Logs)
                {
                    if (!filter.IsEnabled(log.Meta)) continue;
                    if (log.Level < LogLevel) continue;
                    if (string.IsNullOrWhiteSpace(log.Message)) continue;

                    if (!_filter.PassFilter(log.Message) &&
                        !_filter.PassFilter(log.Meta.ModuleName))
                        continue;

                    ImGui.TableNextRow();

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
                    ImGui.TextUnformatted(log.Meta.ModuleName);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(Debug.EnumCache<LogLevel>.GetName(log.Level));

                    ImGui.TableNextColumn();
                    _stringBuilder.Clear();
                    _stringBuilder.AppendFormat("{0}: {1}\0",
                        Debug.PathCache.GetFileName(log.Meta.FilePath), log.Meta.LineNumber);
                    ImGui.TextUnformatted(_stringBuilder.AsSpan());

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(log.Meta.MemberName);

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(log.Message);

                    ImGui.PopStyleColor();
                }
            }

            ImGui.PopFont();

            ImGui.EndTable();
        }
    }

    public void Dispose()
    {
        _stringBuilder.Dispose();
    }
}